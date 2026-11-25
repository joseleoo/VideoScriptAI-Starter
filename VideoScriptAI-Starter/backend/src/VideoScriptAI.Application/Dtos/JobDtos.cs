namespace VideoScriptAI.Application.Dtos;

public sealed record JobDto(
    Guid Id,
    string OriginalFileName,
    string Language,
    string DetectedLanguage,
    string Model,
    string Status,
    int Progress,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string FullText,
    IReadOnlyList<TranscriptSegmentDto> Segments);

public sealed record TranscriptSegmentDto(
    double StartSeconds,
    double EndSeconds,
    string Text);

public sealed record UpdateTranscriptRequest(
    IReadOnlyList<TranscriptSegmentDto> Segments);

public sealed record ExportFileDto(
    string FileName,
    string ContentType,
    string Path);

public sealed record JobProgressNotification(
    Guid JobId,
    string Status,
    int Progress,
    string? Message);

public sealed record UploadedVideo(
    string FileName,
    Stream Content);
