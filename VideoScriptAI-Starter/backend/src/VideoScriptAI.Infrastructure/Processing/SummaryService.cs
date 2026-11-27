using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class SummaryService(
    IEnumerable<ISummaryProvider> providers,
    SummaryOptions options) : ISummaryService
{
    public async Task<string> GenerateAsync(
        SummaryProvider provider,
        string transcript,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var selected = providers.FirstOrDefault(item => item.Provider == provider)
            ?? throw new InvalidOperationException("Proveedor de resumen no disponible: " + provider);

        if (!selected.IsAvailable)
            throw new InvalidOperationException(BuildUnavailableMessage(provider));

        return await selected.GenerateAsync(Truncate(transcript), cancellationToken);
    }

    public IReadOnlyList<SummaryProviderInfoDto> ListProviders()
    {
        var defaultProvider = options.GetDefaultProvider();
        return providers
            .Select(provider => new SummaryProviderInfoDto(
                provider.Provider.ToString(),
                GetLabel(provider.Provider),
                provider.IsAvailable,
                provider.Provider == defaultProvider))
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Provider)
            .ToArray();
    }

    private string Truncate(string transcript)
    {
        var limit = Math.Max(1000, options.MaxTranscriptCharacters);
        if (transcript.Length <= limit)
            return transcript;

        var headLength = (int)(limit * 0.75d);
        var tailLength = limit - headLength;
        return transcript[..headLength]
            + "\n\n[... parte intermedia omitida ...]\n\n"
            + transcript[^tailLength..];
    }

    private static string BuildUnavailableMessage(SummaryProvider provider) => provider switch
    {
        SummaryProvider.OpenAI => "Configura VIDEOSCRIPTAI_OPENAI_API_KEY para usar OpenAI.",
        SummaryProvider.Ollama => "Configura VIDEOSCRIPTAI_OLLAMA_URL para usar Ollama.",
        _ => "El proveedor de resumen no está disponible."
    };

    private static string GetLabel(SummaryProvider provider) => provider switch
    {
        SummaryProvider.Ollama => "Ollama · modelo local",
        SummaryProvider.OpenAI => "OpenAI · API en línea",
        SummaryProvider.Extractive => "Extractivo · sin servicios externos",
        _ => provider.ToString()
    };
}
