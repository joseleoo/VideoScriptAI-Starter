using System.Text.Json;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class OllamaSummaryProvider(
    IHttpClientFactory httpClientFactory,
    SummaryOptions options) : ISummaryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SummaryProvider Provider => SummaryProvider.Ollama;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(options.OllamaBaseUrl);

    public async Task<string> GenerateAsync(string transcript, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        if (!IsAvailable)
            throw new InvalidOperationException("Ollama no está configurado.");

        var client = httpClientFactory.CreateClient("VideoScriptAI.Summary");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        var body = JsonSerializer.Serialize(new
        {
            model = options.OllamaModel,
            prompt = PromptFactory.Build(transcript),
            stream = false
        }, JsonOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.OllamaBaseUrl.TrimEnd('/') + "/"), "api/generate"));
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama devolvió {(int)response.StatusCode}: {Truncate(content, 400)}");

        using var payload = JsonDocument.Parse(content);
        if (!payload.RootElement.TryGetProperty("response", out var text))
            throw new InvalidOperationException("Ollama no devolvió un resumen.");

        var summary = text.GetString();
        if (string.IsNullOrWhiteSpace(summary))
            throw new InvalidOperationException("Ollama devolvió un resumen vacío.");

        return summary.Trim();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
