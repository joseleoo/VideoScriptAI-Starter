namespace VideoScriptAI.Domain.Entities;

public sealed class MeetingSegment
{
    private MeetingSegment() { }

    public MeetingSegment(Guid meetingId, int position, double startSeconds, double endSeconds, string text, string speaker)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        if (startSeconds < 0) throw new ArgumentOutOfRangeException(nameof(startSeconds));
        if (endSeconds < startSeconds) throw new ArgumentOutOfRangeException(nameof(endSeconds));

        MeetingId = meetingId;
        Position = position;
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        Text = text.Trim();
        Speaker = speaker.Trim();
    }

    public Guid Id { get; private set; }
    public Guid MeetingId { get; private set; }
    public int Position { get; private set; }
    public double StartSeconds { get; private set; }
    public double EndSeconds { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string Speaker { get; private set; } = string.Empty;

    public void Update(string text, string speaker)
    {
        Text = text.Trim();
        Speaker = speaker.Trim();
    }
}
