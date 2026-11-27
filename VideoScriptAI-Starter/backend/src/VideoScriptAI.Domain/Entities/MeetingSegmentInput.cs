namespace VideoScriptAI.Domain.Entities;

public sealed class MeetingSegmentInput(
    int position,
    double startSeconds,
    double endSeconds,
    string text,
    string speaker)
{
    public int Position { get; } = position;
    public double StartSeconds { get; } = startSeconds;
    public double EndSeconds { get; } = endSeconds;
    public string Text { get; } = text;
    public string Speaker { get; } = speaker;
}
