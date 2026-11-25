namespace VideoScriptAI.Domain.Entities;

public sealed class TranscriptSegment
{
    private TranscriptSegment() { }

    public TranscriptSegment(Guid jobId, int position, double startSeconds, double endSeconds, string text)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        if (startSeconds < 0) throw new ArgumentOutOfRangeException(nameof(startSeconds));
        if (endSeconds < startSeconds) throw new ArgumentOutOfRangeException(nameof(endSeconds));

        JobId = jobId;
        Position = position;
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        Text = text.Trim();
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int Position { get; private set; }
    public double StartSeconds { get; private set; }
    public double EndSeconds { get; private set; }
    public string Text { get; private set; } = string.Empty;
}
