namespace VideoScriptAI.Domain.Enums;

public enum TranscriptionStatus
{
    Pending,
    Validating,
    ExtractingAudio,
    Transcribing,
    Exporting,
    Completed,
    Failed,
    Cancelled
}
