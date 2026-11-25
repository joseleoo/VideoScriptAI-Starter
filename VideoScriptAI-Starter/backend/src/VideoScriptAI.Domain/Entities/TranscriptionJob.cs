using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Domain.Entities;

public sealed class TranscriptionJob
{
    private TranscriptionJob() { } // EF Core

    public TranscriptionJob(string originalFileName, string storedVideoPath, string language,
        TranscriptionModel model)
    {
        Id = Guid.NewGuid();
        OriginalFileName = originalFileName;
        StoredVideoPath = storedVideoPath;
        Language = language;
        Model = model;
        Status = TranscriptionStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoredVideoPath { get; private set; } = string.Empty;
    public string Language { get; private set; } = "auto";
    public TranscriptionModel Model { get; private set; }
    public TranscriptionStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void UpdateProgress(TranscriptionStatus status, int progress)
    {
        if (progress is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(progress));
        Status = status;
        Progress = progress;
    }

    public void Complete()
    {
        Status = TranscriptionStatus.Completed;
        Progress = 100;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string message)
    {
        Status = TranscriptionStatus.Failed;
        ErrorMessage = message;
    }

    public void Cancel() => Status = TranscriptionStatus.Cancelled;
}
