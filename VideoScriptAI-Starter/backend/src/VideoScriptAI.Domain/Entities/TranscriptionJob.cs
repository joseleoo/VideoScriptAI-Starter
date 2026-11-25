using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Domain.Entities;

public sealed class TranscriptionJob
{
    private TranscriptionJob() { } // EF Core

    public TranscriptionJob(string originalFileName, string storedVideoPath, string language,
        TranscriptionModel model)
        : this(originalFileName, storedVideoPath, language, model, null)
    {
    }

    public TranscriptionJob(string originalFileName, string storedVideoPath, string language,
        TranscriptionModel model, string? sourceUrl)
    {
        Id = Guid.NewGuid();
        OriginalFileName = originalFileName;
        StoredVideoPath = storedVideoPath;
        Language = language;
        Model = model;
        Source = string.IsNullOrWhiteSpace(sourceUrl) ? VideoSource.Upload : VideoSource.Url;
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        Status = TranscriptionStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoredVideoPath { get; private set; } = string.Empty;
    public VideoSource Source { get; private set; }
    public string? SourceUrl { get; private set; }
    public string Language { get; private set; } = "auto";
    public TranscriptionModel Model { get; private set; }
    public TranscriptionStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string DetectedLanguage { get; private set; } = string.Empty;
    public string FullText { get; private set; } = string.Empty;
    public ICollection<TranscriptSegment> Segments { get; private set; } = new List<TranscriptSegment>();
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void UpdateProgress(TranscriptionStatus status, int progress)
    {
        if (progress is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(progress));
        Status = status;
        Progress = progress;
    }

    public void SetTranscript(string detectedLanguage, IReadOnlyList<TranscriptSegmentInput> segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectedLanguage);
        ArgumentNullException.ThrowIfNull(segments);

        DetectedLanguage = detectedLanguage.Trim();
        Segments.Clear();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            Segments.Add(new TranscriptSegment(
                Id,
                index,
                segment.StartSeconds,
                segment.EndSeconds,
                segment.Text));
        }

        FullText = string.Join(' ', Segments
            .OrderBy(x => x.Position)
            .Select(x => x.Text)
            .Where(x => x.Length > 0));
    }

    public void Complete()
    {
        Status = TranscriptionStatus.Completed;
        Progress = 100;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Status = TranscriptionStatus.Failed;
        ErrorMessage = message;
    }

    public void Cancel()
    {
        Status = TranscriptionStatus.Cancelled;
        CompletedAtUtc ??= DateTimeOffset.UtcNow;
    }
}
