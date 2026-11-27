using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Application.Configuration;

public sealed class SummaryOptions
{
    public string DefaultProvider { get; set; } = "Ollama";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public int MaxTranscriptCharacters { get; set; } = 20000;
    public int TimeoutSeconds { get; set; } = 120;

    public SummaryProvider GetDefaultProvider()
    {
        if (!Enum.TryParse<SummaryProvider>(DefaultProvider, ignoreCase: true, out var provider))
            throw new InvalidOperationException("Unsupported default summary provider: " + DefaultProvider);

        return provider;
    }
}
