using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Domain.Entities;

public sealed class MeetingNote
{
    private MeetingNote() { } // EF Core

    public MeetingNote(string title, MeetingSource source, string language, SummaryProvider summaryProvider)
    {
        Id = Guid.NewGuid();
        Title = NormalizeTitle(title);
        Source = source;
        Language = language;
        SummaryProvider = summaryProvider;
        Status = source == MeetingSource.Live ? MeetingStatus.Recording : MeetingStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public MeetingSource Source { get; private set; }
    public string Language { get; private set; } = "auto";
    public string DetectedLanguage { get; private set; } = string.Empty;
    public string? AudioPath { get; private set; }
    public SummaryProvider SummaryProvider { get; private set; }
    public MeetingStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string FullText { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public ICollection<MeetingSegment> Segments { get; private set; } = new List<MeetingSegment>();
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void AttachAudio(string audioPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        AudioPath = audioPath;
        Status = MeetingStatus.Pending;
        Progress = 0;
        ErrorMessage = null;
    }

    public void UpdateProgress(MeetingStatus status, int progress)
    {
        if (progress is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(progress));
        Status = status;
        Progress = progress;
    }

    public void SetTranscript(string detectedLanguage, IReadOnlyList<MeetingSegmentInput> segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectedLanguage);
        ArgumentNullException.ThrowIfNull(segments);

        DetectedLanguage = detectedLanguage.Trim();
        Segments.Clear();

        foreach (var input in segments)
        {
            Segments.Add(new MeetingSegment(
                Id,
                input.Position,
                input.StartSeconds,
                input.EndSeconds,
                input.Text,
                input.Speaker));
        }

        RebuildFullText();
    }

    public void UpdateSegments(IReadOnlyList<MeetingSegmentInput> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var byPosition = Segments.ToDictionary(segment => segment.Position);
        foreach (var input in segments)
        {
            if (byPosition.TryGetValue(input.Position, out var segment))
                segment.Update(input.Text, input.Speaker);
        }

        RebuildFullText();
    }

    public void SetSummary(string summary, SummaryProvider provider)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary.Trim();
        SummaryProvider = provider;
    }

    public void Complete()
    {
        Status = MeetingStatus.Completed;
        Progress = 100;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Status = MeetingStatus.Failed;
        ErrorMessage = message;
        CompletedAtUtc ??= DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = MeetingStatus.Cancelled;
        CompletedAtUtc ??= DateTimeOffset.UtcNow;
    }

    public void Retry()
    {
        if (string.IsNullOrWhiteSpace(AudioPath))
            throw new InvalidOperationException("La reunión no tiene audio para volver a procesar.");

        Status = MeetingStatus.Pending;
        Progress = 0;
        ErrorMessage = null;
        CompletedAtUtc = null;
        DetectedLanguage = string.Empty;
        Summary = string.Empty;
        Segments.Clear();
        RebuildFullText();
    }

    private void RebuildFullText()
    {
        FullText = string.Join(Environment.NewLine, Segments
            .OrderBy(segment => segment.Position)
            .Select(segment => string.IsNullOrWhiteSpace(segment.Speaker)
                ? segment.Text
                : $"{segment.Speaker}: {segment.Text}")
            .Where(line => line.Length > 0));
    }

    private static string NormalizeTitle(string title)
    {
        var value = title?.Trim() ?? string.Empty;
        if (value.Length == 0)
            value = $"Reunión {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}";

        return value.Length <= 200 ? value : value[..200];
    }
}
