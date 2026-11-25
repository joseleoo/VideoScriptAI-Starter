using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface ITextToSpeechService
{
    Task<IReadOnlyList<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken);
    Task<SynthesizedSpeechDto> SynthesizeAsync(
        SynthesizeSpeechRequest request,
        CancellationToken cancellationToken);
}
