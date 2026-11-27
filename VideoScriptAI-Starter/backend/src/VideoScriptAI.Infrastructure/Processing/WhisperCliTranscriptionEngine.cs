using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed partial class WhisperCliTranscriptionEngine(
    IProcessRunner processRunner,
    EngineOptions options) : ITranscriptionEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AudioPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Language);

        if (!File.Exists(request.AudioPath))
            throw new FileNotFoundException("Audio file not found.", request.AudioPath);
        if (!File.Exists(request.ModelPath))
            throw new FileNotFoundException("Whisper model not found.", request.ModelPath);
        if (string.IsNullOrWhiteSpace(options.WhisperPath))
            throw new InvalidOperationException("whisper.cpp path is not configured.");
        if (!File.Exists(options.WhisperPath))
            throw new FileNotFoundException("whisper.cpp executable not found.", options.WhisperPath);
        if (options.WhisperThreads is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(options.WhisperThreads), "Thread count must be between 1 and 32.");

        var language = NormalizeLanguage(request.Language);
        var outputPrefix = Path.Combine(
            Path.GetTempPath(),
            "videoscriptai-whisper-" + Guid.NewGuid().ToString("N"));
        var jsonPath = outputPrefix + ".json";

        string[] arguments =
        {
            "-m", request.ModelPath,
            "-f", request.AudioPath,
            "-l", language,
            "-t", options.WhisperThreads.ToString(),
            "-ng",
            "-pp",
            "-oj",
            "-of", outputPrefix
        };

        try
        {
            progress?.Report(5);
            var output = new InlineProgress<string>(line =>
            {
                var match = ProgressPattern().Match(line);
                if (match.Success && int.TryParse(match.Groups["value"].Value, out var value))
                    progress?.Report(Math.Clamp(5 + value * 85 / 100, 5, 90));
            });
            var result = await processRunner.RunAsync(options.WhisperPath, arguments, cancellationToken, output);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"whisper.cpp transcription failed (exit code {result.ExitCode}): " +
                    $"{result.StandardError.Trim()} {result.StandardOutput.Trim()}".Trim());
            }
            if (!File.Exists(jsonPath))
                throw new InvalidOperationException("whisper.cpp did not produce JSON output.");

            progress?.Report(90);
            var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var payload = JsonSerializer.Deserialize<WhisperOutput>(json, JsonOptions)
                ?? throw new InvalidOperationException("whisper.cpp JSON output was empty.");

            var segments = payload.Transcription
                .Select(segment => new TranscriptSegment(
                    segment.Offsets.From / 1000.0,
                    segment.Offsets.To / 1000.0,
                    segment.Text.Trim()))
                .Where(segment => segment.Text.Length > 0)
                .ToArray();

            var fullText = string.Join(' ', segments.Select(segment => segment.Text));
            var detectedLanguage = string.IsNullOrWhiteSpace(payload.Result.Language)
                ? language
                : payload.Result.Language;

            progress?.Report(100);
            return new TranscriptionResult(detectedLanguage, fullText, segments);
        }
        finally
        {
            if (File.Exists(jsonPath))
                File.Delete(jsonPath);
        }
    }

    private static string NormalizeLanguage(string language)
    {
        var value = language.Trim();
        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return "auto";
        if (!LanguagePattern().IsMatch(value))
            throw new ArgumentException("Invalid language code: " + language, nameof(language));
        return value.ToLowerInvariant();
    }

    [GeneratedRegex(@"progress\s*=\s*(?<value>\d{1,3})\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex ProgressPattern();

    [GeneratedRegex(@"^[a-z]{2}(?:-[a-z0-9]{2,8})?$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePattern();

    private sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    private sealed class WhisperOutput
    {
        [JsonPropertyName("result")]
        public WhisperResult Result { get; set; } = new();

        [JsonPropertyName("transcription")]
        public List<WhisperSegment> Transcription { get; set; } = [];
    }

    private sealed class WhisperResult
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;
    }

    private sealed class WhisperSegment
    {
        [JsonPropertyName("offsets")]
        public WhisperOffsets Offsets { get; set; } = new();

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class WhisperOffsets
    {
        [JsonPropertyName("from")]
        public long From { get; set; }

        [JsonPropertyName("to")]
        public long To { get; set; }
    }
}
