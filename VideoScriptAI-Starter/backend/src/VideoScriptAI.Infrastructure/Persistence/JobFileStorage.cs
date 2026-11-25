using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class JobFileStorage(StorageOptions options) : IJobFileStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi"
    };

    public async Task<string> SaveUploadAsync(UploadedVideo video, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);

        var extension = Path.GetExtension(video.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported video extension: " + extension);

        var header = new byte[12];
        var headerLength = await video.Content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        ValidateHeader(extension, header, headerLength);

        Directory.CreateDirectory(options.UploadsPath);
        var storedName = BuildStoredName(video.FileName, extension);
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
                var bytesRead = await video.Content.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > options.MaxUploadBytes)
                    throw new InvalidDataException("The uploaded video exceeds the configured size limit.");

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
            throw new InvalidDataException("The uploaded file is too small to be a valid video.");

        switch (extension)
        {
            case ".mp4":
            case ".mov":
                if (header[4] != (byte)'f' || header[5] != (byte)'t' ||
                    header[6] != (byte)'y' || header[7] != (byte)'p')
                {
                    throw new InvalidDataException("The uploaded MP4/MOV file has an invalid header.");
                }

                break;
            case ".mkv":
                if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)
                    throw new InvalidDataException("The uploaded MKV file has an invalid header.");

                break;
            case ".avi":
                if (header[0] != (byte)'R' || header[1] != (byte)'I' ||
                    header[2] != (byte)'F' || header[3] != (byte)'F' ||
                    header[8] != (byte)'A' || header[9] != (byte)'V' ||
                    header[10] != (byte)'I' || header[11] != (byte)' ')
                {
                    throw new InvalidDataException("The uploaded AVI file has an invalid header.");
                }

                break;
        }
    }

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
