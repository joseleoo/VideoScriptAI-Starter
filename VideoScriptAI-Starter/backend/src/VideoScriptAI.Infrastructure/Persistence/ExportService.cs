using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Application.Services;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class ExportService(StorageOptions options) : IExportService
{
    public async Task<ExportFileDto> ExportAsync(
        TranscriptionJob job,
        string format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var extension = normalizedFormat switch
        {
            "txt" => "txt",
            "srt" => "srt",
            "vtt" => "vtt",
            "json" => "json",
            _ => throw new ArgumentException("Unsupported export format: " + format, nameof(format))
        };

        var contentType = extension switch
        {
            "txt" => "text/plain; charset=utf-8",
            "srt" => "application/x-subrip; charset=utf-8",
            "vtt" => "text/vtt; charset=utf-8",
            _ => "application/json; charset=utf-8"
        };

        var content = TranscriptExporter.Format(job, normalizedFormat);
        Directory.CreateDirectory(options.ExportsPath);

        var fileBaseName = Path.GetFileNameWithoutExtension(job.OriginalFileName);
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = fileBaseName
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray();
        var safeName = new string(safeCharacters).Trim();
        if (safeName.Length == 0)
            safeName = "transcript";
        if (safeName.Length > 80)
            safeName = safeName[..80];

        var fileName = $"{job.Id:N}_{safeName}.{extension}";
        var outputPath = Path.GetFullPath(Path.Combine(options.ExportsPath, fileName));
        var directoryPrefix = options.ExportsPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved export path is outside the storage directory.");

        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        return new ExportFileDto(fileName, contentType, outputPath);
    }
}
