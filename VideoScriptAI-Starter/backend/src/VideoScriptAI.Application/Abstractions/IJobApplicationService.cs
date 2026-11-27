using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IJobApplicationService
{
    Task<JobDto> CreateJobAsync(
        UploadedVideo video,
        string language,
        string model,
        CancellationToken cancellationToken);

    Task<JobDto> CreateJobFromUrlAsync(
        VideoUrl videoUrl,
        string language,
        string model,
        CancellationToken cancellationToken);

    Task<JobDto?> GetJobAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobDto>> ListJobsAsync(CancellationToken cancellationToken);
    Task<bool> CancelJobAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DeleteJobAsync(Guid id, CancellationToken cancellationToken);

    Task<JobDto?> UpdateTranscriptAsync(
        Guid id,
        UpdateTranscriptRequest request,
        CancellationToken cancellationToken);

    Task<ExportFileDto?> PrepareExportAsync(
        Guid id,
        string format,
        CancellationToken cancellationToken);
}
