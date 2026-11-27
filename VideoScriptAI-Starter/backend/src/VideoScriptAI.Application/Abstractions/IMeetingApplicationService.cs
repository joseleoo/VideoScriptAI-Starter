using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IMeetingApplicationService
{
    Task<MeetingDto> CreateFromUploadAsync(
        UploadedAudio audio,
        string title,
        string language,
        string summaryProvider,
        CancellationToken cancellationToken);

    Task<MeetingDto> StartRecordingAsync(
        string title,
        string language,
        string summaryProvider,
        RecordingCaptureSource captureSource,
        CancellationToken cancellationToken);

    Task<MeetingDto?> StopRecordingAsync(CancellationToken cancellationToken);
    RecordingStatusDto GetRecordingStatus();
    Task<MeetingDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeetingDto>> ListAsync(CancellationToken cancellationToken);
    Task<MeetingDto?> UpdateSegmentsAsync(Guid id, UpdateMeetingSegmentsRequest request, CancellationToken cancellationToken);
    Task<MeetingDto?> UpdateSummaryAsync(Guid id, string summary, CancellationToken cancellationToken);
    Task<MeetingDto?> RegenerateSummaryAsync(Guid id, string? summaryProvider, CancellationToken cancellationToken);
    Task<MeetingDto?> RetryAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<ExportFileDto?> PrepareExportAsync(Guid id, string format, CancellationToken cancellationToken);
}
