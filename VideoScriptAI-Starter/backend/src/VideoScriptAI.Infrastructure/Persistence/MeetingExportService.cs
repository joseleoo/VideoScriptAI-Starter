using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Application.Services;
using VideoScriptAI.Domain.Entities;

namespace VideoScriptAI.Infrastructure.Persistence;

public sealed class MeetingExportService(StorageOptions options) : IMeetingExportService
{
    public async Task<ExportFileDto> ExportAsync(
        MeetingNote meeting,
        string format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var extension = normalizedFormat switch
        {
            "md" => "md",
            "txt" => "txt",
            "json" => "json",
            _ => throw new ArgumentException("Unsupported export format: " + format, nameof(format))
        };

        var contentType = extension switch
        {
            "md" => "text/markdown; charset=utf-8",
            "txt" => "text/plain; charset=utf-8",
            _ => "application/json; charset=utf-8"
        };

        var content = MeetingExporter.Format(meeting, normalizedFormat);
        Directory.CreateDirectory(options.ExportsPath);

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = meeting.Title
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray();
        var safeName = new string(safeCharacters).Trim();
        if (safeName.Length == 0)
            safeName = "meeting-notes";
        if (safeName.Length > 80)
            safeName = safeName[..80];

        var fileName = $"{meeting.Id:N}_{safeName}.{extension}";
        var outputPath = Path.GetFullPath(Path.Combine(options.ExportsPath, fileName));
        var prefix = options.ExportsPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved export path is outside the storage directory.");

        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        return new ExportFileDto(fileName, contentType, outputPath);
    }
}
