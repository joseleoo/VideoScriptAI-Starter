using System.Net;
using System.Net.Http.Headers;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class JobFileStorage(StorageOptions options, IHttpClientFactory httpClientFactory) : IJobFileStorage
{
    private const int MaxRedirects = 5;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi"
    };

    private static readonly IReadOnlyDictionary<string, string> ContentTypeExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["video/mp4"] = ".mp4",
            ["video/x-m4v"] = ".mp4",
            ["video/quicktime"] = ".mov",
            ["video/x-matroska"] = ".mkv",
            ["video/x-msvideo"] = ".avi"
        };

    public async Task<string> SaveUploadAsync(UploadedVideo video, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);

        var extension = NormalizeExtension(Path.GetExtension(video.FileName));
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported video extension: " + extension);

        return await WriteVideoAsync(extension, video.FileName, video.Content, cancellationToken);
    }

    public async Task<string> SaveUrlAsync(VideoUrl videoUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videoUrl);

        var location = ParseUrl(videoUrl.Url);
        var client = httpClientFactory.CreateClient("VideoScriptAI.VideoDownload");
        client.Timeout = TimeSpan.FromMinutes(30);

        for (var redirectCount = 0; ; redirectCount++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, location);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("video/*"));
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (IsRedirect(response.StatusCode))
                {
                    if (response.Headers.Location is null)
                        throw new InvalidOperationException("The video URL returned a redirect without a target.");
                    if (redirectCount >= MaxRedirects)
                        throw new InvalidOperationException("The video URL exceeded the redirect limit.");

                    location = ParseRedirect(location, response.Headers.Location);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > options.MaxUploadBytes)
                    throw new InvalidDataException("The video at this URL exceeds the configured size limit.");

                var extension = ResolveExtension(location, response.Content.Headers.ContentType?.MediaType);
                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await WriteVideoAsync(extension, "video-from-url", content, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new InvalidOperationException(
                    "Could not download the video URL: " + exception.Message,
                    exception);
            }
        }
    }

    private async Task<string> WriteVideoAsync(
        string extension,
        string requestedName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var headerLength = 0;
        while (headerLength < header.Length)
        {
            var read = await content.ReadAsync(header.AsMemory(headerLength), cancellationToken);
            if (read == 0)
                break;

            headerLength += read;
        }

        ValidateHeader(extension, header, headerLength);

        Directory.CreateDirectory(options.UploadsPath);
        var storedName = BuildStoredName(requestedName, extension);
        var outputPath = Path.GetFullPath(Path.Combine(options.UploadsPath, storedName));
        var directoryPrefix = options.UploadsPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved upload path is outside the storage directory.");

        try
        {
            await using var output = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await output.WriteAsync(header.AsMemory(0, headerLength), cancellationToken);
            long totalBytes = headerLength;
            var buffer = new byte[81920];

            while (true)
            {
                var bytesRead = await content.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > options.MaxUploadBytes)
                    throw new InvalidDataException("The video exceeds the configured size limit.");

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            return outputPath;
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
    }

    private static void ValidateHeader(string extension, byte[] header, int length)
    {
        if (length < 12)
            throw new InvalidDataException("The video file is too small to be valid.");

        switch (extension)
        {
            case ".mp4":
            case ".mov":
                if (header[4] != (byte)'f' || header[5] != (byte)'t' ||
                    header[6] != (byte)'y' || header[7] != (byte)'p')
                {
                    throw new InvalidDataException("The MP4/MOV video has an invalid header.");
                }

                break;
            case ".mkv":
                if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)
                    throw new InvalidDataException("The MKV video has an invalid header.");

                break;
            case ".avi":
                if (header[0] != (byte)'R' || header[1] != (byte)'I' ||
                    header[2] != (byte)'F' || header[3] != (byte)'F' ||
                    header[8] != (byte)'A' || header[9] != (byte)'V' ||
                    header[10] != (byte)'I' || header[11] != (byte)' ')
                {
                    throw new InvalidDataException("The AVI video has an invalid header.");
                }

                break;
        }
    }

    private static Uri ParseUrl(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > 2048)
            throw new ArgumentException("The video URL is too long.", nameof(value));

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The video URL must use HTTP or HTTPS.", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("The video URL must not contain credentials.", nameof(value));

        return uri;
    }

    private static Uri ParseRedirect(Uri current, Uri location)
    {
        var resolved = location.IsAbsoluteUri ? location : new Uri(current, location);
        return ParseUrl(resolved.AbsoluteUri);
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.Redirect
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static string ResolveExtension(Uri uri, string? contentType)
    {
        var pathExtension = NormalizeExtension(Path.GetExtension(Uri.UnescapeDataString(uri.AbsolutePath)));
        if (AllowedExtensions.Contains(pathExtension))
            return pathExtension;

        var mediaType = contentType?.Split(';')[0].Trim();
        if (mediaType is not null && ContentTypeExtensions.TryGetValue(mediaType, out var mapped))
            return mapped;

        throw new InvalidOperationException(
            "Unsupported video URL type: " + (string.IsNullOrWhiteSpace(mediaType) ? "unknown" : mediaType));
    }

    private static string NormalizeExtension(string extension) =>
        string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim().ToLowerInvariant();

    private static string BuildStoredName(string fileName, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = baseName
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray();

        var safeName = new string(safeCharacters).Trim();
        if (safeName.Length == 0)
            safeName = "video";
        if (safeName.Length > 100)
            safeName = safeName[..100];

        return $"{Guid.NewGuid():N}_{safeName}{extension}";
    }
}
