namespace VideoScriptAI.Domain.Entities;

public readonly record struct TranscriptSegmentInput(
    double StartSeconds,
    double EndSeconds,
    string Text);
