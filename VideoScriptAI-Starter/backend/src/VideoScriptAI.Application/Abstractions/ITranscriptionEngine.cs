namespace VideoScriptAI.Application.Abstractions;

public sealed record TranscriptionRequest(
    string AudioPath,
    string ModelPath,
    string Language);

public sealed record TranscriptSegment(
    double StartSeconds,
    double EndSeconds,
    string Text);

public sealed record TranscriptionResult(
    string Language,
    string FullText,
    IReadOnlyList<TranscriptSegment> Segments);

public interface ITranscriptionEngine
{
    Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<int>? progress,
        CancellationToken cancellationToken);
}
