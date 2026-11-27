using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Dtos;

public sealed record MeetingDto(
    Guid Id,
    string Title,
    string Source,
    string Language,
    string DetectedLanguage,
    string SummaryProvider,
    string Status,
    int Progress,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string FullText,
    string Summary,
    IReadOnlyList<MeetingSegmentDto> Segments);

public sealed record MeetingSegmentDto(
    int Position,
    double StartSeconds,
    double EndSeconds,
    string Text,
    string Speaker);

public sealed record CreateMeetingRequest(
    string? Title,
    string? Language,
    string? SummaryProvider,
    string? CaptureSource);

public sealed record UpdateMeetingSegmentsRequest(
    IReadOnlyList<MeetingSegmentDto> Segments);

public sealed record UpdateMeetingSummaryRequest(
    string? Summary);

public sealed record RegenerateSummaryRequest(
    string? SummaryProvider);

public sealed record MeetingProgressNotification(
    Guid MeetingId,
    string Status,
    int Progress,
    string? Message);

public sealed record RecordingStatusDto(
    bool IsRecording,
    Guid? MeetingId,
    DateTimeOffset? StartedAtUtc,
    long BytesWritten);

public sealed record SummaryProviderInfoDto(
    string Provider,
    string Label,
    bool Available,
    bool IsDefault);

public sealed record UploadedAudio(
    string FileName,
    Stream Content);
