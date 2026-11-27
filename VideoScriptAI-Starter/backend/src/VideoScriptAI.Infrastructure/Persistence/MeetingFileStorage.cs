using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class MeetingFileStorage(StorageOptions options) : IMeetingFileStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".webm", ".flac", ".wma",
        ".mp4", ".mov", ".mkv"
    };

    public async Task<string> SaveUploadAsync(UploadedAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var extension = Path.GetExtension(audio.FileName).Trim().ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported recording extension: " + extension);

        var directory = options.MeetingsPath;
        Directory.CreateDirectory(directory);

        var storedName = BuildStoredName(audio.FileName, extension);
        var outputPath = Path.GetFullPath(Path.Combine(directory, storedName));
        EnsureInsideDirectory(outputPath, directory);

        try
        {
            await using var output = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await audio.Content.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > options.MaxUploadBytes)
                    throw new InvalidDataException("The recording exceeds the configured size limit.");

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (totalBytes == 0)
                throw new InvalidDataException("The recording file is empty.");

            return outputPath;
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
    }

    public string GetRecordingPath(Guid meetingId)
    {
        var path = Path.GetFullPath(Path.Combine(options.MeetingsPath, $"{meetingId:N}.wav"));
        EnsureInsideDirectory(path, options.MeetingsPath);
        return path;
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        EnsureInsideDirectory(fullPath, options.MeetingsPath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private static void EnsureInsideDirectory(string path, string directory)
    {
        var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved recording path is outside the storage directory.");
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
            safeName = "meeting";
        if (safeName.Length > 100)
            safeName = safeName[..100];

        return $"{Guid.NewGuid():N}_{safeName}{extension}";
    }
}
