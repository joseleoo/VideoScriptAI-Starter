using System.Text.RegularExpressions;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Entities;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Services;

public sealed partial class TranscriptionJobService(
    IJobRepository repository,
    IJobFileStorage fileStorage,
    IJobQueue queue,
    IJobCancellationRegistry cancellation,
    IJobProgressNotifier notifier,
    IExportService exporter) : IJobApplicationService
{
    public async Task<JobDto> CreateJobAsync(
        UploadedVideo video,
        string language,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);

        var normalizedLanguage = NormalizeLanguage(language);
        var parsedModel = ParseModel(model);
        var storedVideoPath = await fileStorage.SaveUploadAsync(video, cancellationToken);
        var job = new TranscriptionJob(video.FileName, storedVideoPath, normalizedLanguage, parsedModel);

        await repository.AddAsync(job, cancellationToken);
        await queue.EnqueueAsync(job.Id, cancellationToken);

        return Map(job);
    }

    public async Task<JobDto> CreateJobFromUrlAsync(
        VideoUrl videoUrl,
        string language,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videoUrl);

        var normalizedLanguage = NormalizeLanguage(language);
        var parsedModel = ParseModel(model);
        var storedVideoPath = await fileStorage.SaveUrlAsync(videoUrl, cancellationToken);
        var job = new TranscriptionJob(
            GetUrlFileName(videoUrl.Url),
            storedVideoPath,
            normalizedLanguage,
            parsedModel,
            videoUrl.Url);

        await repository.AddAsync(job, cancellationToken);
        await queue.EnqueueAsync(job.Id, cancellationToken);

        return Map(job);
    }

    public async Task<JobDto?> GetJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await repository.GetAsync(id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<IReadOnlyList<JobDto>> ListJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await repository.ListAsync(cancellationToken);
        return jobs.Select(Map).ToArray();
    }

    public async Task<bool> CancelJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await repository.GetAsync(id, cancellationToken);
        if (job is null)
            return false;

        if (job.Status is TranscriptionStatus.Completed
            or TranscriptionStatus.Failed
            or TranscriptionStatus.Cancelled)
        {
            return true;
        }

        cancellation.Cancel(id);
        job.Cancel();
        await repository.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(
            new JobProgressNotification(id, job.Status.ToString(), job.Progress, "Cancellation requested."),
            cancellationToken);

        return true;
    }

    public async Task<JobDto?> UpdateTranscriptAsync(
        Guid id,
        UpdateTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = await repository.GetAsync(id, cancellationToken);
        if (job is null)
            return null;

        job.SetTranscript(
            string.IsNullOrWhiteSpace(job.DetectedLanguage) ? job.Language : job.DetectedLanguage,
            request.Segments.Select(segment =>
                new TranscriptSegmentInput(segment.StartSeconds, segment.EndSeconds, segment.Text)).ToArray());

        await repository.SaveChangesAsync(cancellationToken);
        return Map(job);
    }

    public async Task<ExportFileDto?> PrepareExportAsync(
        Guid id,
        string format,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetAsync(id, cancellationToken);
        if (job is null)
            return null;

        return await exporter.ExportAsync(job, format, cancellationToken);
    }

    private static JobDto Map(TranscriptionJob job) => new(
        job.Id,
        job.OriginalFileName,
        job.Source.ToString(),
        job.SourceUrl,
        job.Language,
        job.DetectedLanguage,
        job.Model.ToString(),
        job.Status.ToString(),
        job.Progress,
        job.ErrorMessage,
        job.CreatedAtUtc,
        job.CompletedAtUtc,
        job.FullText,
        job.Segments
            .OrderBy(x => x.Position)
            .Select(x => new TranscriptSegmentDto(x.StartSeconds, x.EndSeconds, x.Text))
            .ToArray());

    private static string GetUrlFileName(string url)
    {
        var name = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath))
            : string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            return "video-from-url";

        return name.Length <= 260 ? name : name[..260];
    }

    private static string NormalizeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var value = language.Trim();
        if (!LanguagePattern().IsMatch(value))
            throw new ArgumentException("Invalid language code: " + language, nameof(language));

        return value.ToLowerInvariant();
    }

    private static TranscriptionModel ParseModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!Enum.TryParse<TranscriptionModel>(model, ignoreCase: true, out var parsed))
            throw new ArgumentException("Unsupported transcription model: " + model, nameof(model));

        return parsed;
    }

    [GeneratedRegex(@"^(auto|[a-z]{2}(?:-[a-z0-9]{2,8})?)$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePattern();
}
