using System.Text;
using System.Text.Json;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Domain.Enums;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class OpenAiSummaryProvider(
    IHttpClientFactory httpClientFactory,
    SummaryOptions options) : ISummaryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SummaryProvider Provider => SummaryProvider.OpenAI;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(options.OpenAiApiKey);

    public async Task<string> GenerateAsync(string transcript, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        if (!IsAvailable)
            throw new InvalidOperationException("No se configuró la clave de OpenAI.");

        var client = httpClientFactory.CreateClient("VideoScriptAI.Summary");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        var body = JsonSerializer.Serialize(new
        {
            model = options.OpenAiModel,
            temperature = 0.3,
            messages = new object[]
            {
                new { role = "system", content = PromptFactory.SystemPrompt },
                new { role = "user", content = transcript }
            }
        }, JsonOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.OpenAiBaseUrl.TrimEnd('/') + "/"), "chat/completions"));
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + options.OpenAiApiKey.Trim());
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI devolvió {(int)response.StatusCode}: {Truncate(content, 400)}");

        using var payload = JsonDocument.Parse(content);
        var choices = payload.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("OpenAI no devolvió un resumen.");

        var summary = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(summary))
            throw new InvalidOperationException("OpenAI devolvió un resumen vacío.");

        return summary.Trim();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
