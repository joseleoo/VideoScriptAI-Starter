using System.Runtime.Versioning;
using System.Speech.Synthesis;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class TextToSpeechService(
    StorageOptions storage,
    EngineOptions engine,
    TextToSpeechOptions speech,
    IProcessRunner runner) : ITextToSpeechService
{
    private const int MaxTextLength = 10_000;

    [SupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken)
    {
        var voices = GetWindowsVoices().ToList();

        try
        {
            voices.AddRange(await GetEdgeVoicesAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                          or FileNotFoundException
                                          or OperationCanceledException)
        {
        }

        return voices
            .OrderBy(voice => voice.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [SupportedOSPlatform("windows")]
    public async Task<SynthesizedSpeechDto> SynthesizeAsync(
        SynthesizeSpeechRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new ArgumentException("Text is required.", nameof(request));
        if (text.Length > MaxTextLength)
            throw new ArgumentException($"Text must be {MaxTextLength} characters or fewer.", nameof(request));

        var provider = NormalizeProvider(request.Provider);
        var voiceId = NormalizeVoiceId(request.VoiceId);
        var voices = await GetVoicesAsync(cancellationToken);

        if (!voices.Any(voice => voice.Provider == provider &&
            string.Equals(voice.Id, voiceId, StringComparison.OrdinalIgnoreCase)))
        {
            if (provider == "Edge" && !voices.Any(voice => voice.Provider == "Edge"))
                throw new InvalidOperationException("Edge TTS voices are unavailable. Install edge-tts for Python.");

            throw new ArgumentException($"Unsupported {provider} voice: {voiceId}", nameof(request));
        }

        Directory.CreateDirectory(storage.SpeechPath);
        var fileName = $"speech_{Guid.NewGuid():N}.mp3";
        var outputPath = Path.GetFullPath(Path.Combine(storage.SpeechPath, fileName));
        var directoryPrefix = storage.SpeechPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved speech path is outside the storage directory.");

        try
        {
            if (provider == "Windows")
                await SynthesizeWindowsAsync(text, voiceId, outputPath, cancellationToken);
            else
                await SynthesizeEdgeAsync(text, voiceId, outputPath, cancellationToken);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                throw new InvalidOperationException("Text-to-speech produced an empty audio file.");

            return new SynthesizedSpeechDto(fileName, "audio/mpeg", outputPath);
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<TtsVoiceDto> GetWindowsVoices()
    {
        using var synthesizer = new SpeechSynthesizer();
        return synthesizer.GetInstalledVoices()
            .Where(installed => installed.Enabled)
            .Select(installed => installed.VoiceInfo)
            .Select(info => new TtsVoiceDto(
                "Windows",
                info.Name ?? string.Empty,
                info.Name ?? string.Empty,
                info.Culture?.Name ?? string.Empty,
                info.Gender.ToString()))
            .ToArray();
    }

    private async Task<IReadOnlyList<TtsVoiceDto>> GetEdgeVoicesAsync(CancellationToken cancellationToken)
    {
        var pythonPath = ResolvePythonPath();
        var result = await runner.RunAsync(
            pythonPath,
            ["-m", "edge_tts", "--list-voices"],
            cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException("Could not load Edge TTS voices: " + Describe(result));

        return ParseEdgeVoiceList(result.StandardOutput).ToArray();
    }

    internal static IReadOnlyList<TtsVoiceDto> ParseEdgeVoiceList(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 &&
                           !line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) &&
                           !line.StartsWith('-'))
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(columns => columns.Length >= 2)
            .Select(columns => new TtsVoiceDto(
                "Edge",
                columns[0],
                columns[0],
                GetLocale(columns[0]),
                columns[1]))
            .ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task SynthesizeWindowsAsync(
        string text,
        string voiceId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var wavPath = Path.Combine(Path.GetTempPath(), $"videoscriptai-tts-{Guid.NewGuid():N}.wav");
        try
        {
            using (var synthesizer = new SpeechSynthesizer())
            {
                synthesizer.SelectVoice(voiceId);
                synthesizer.SetOutputToWaveFile(wavPath);
                synthesizer.Speak(text);
            }

            var result = await runner.RunAsync(
                engine.FfmpegPath,
                ["-y", "-i", wavPath, "-codec:a", "libmp3lame", "-qscale:a", "2", outputPath],
                cancellationToken);

            if (result.ExitCode != 0)
                throw new InvalidOperationException("FFmpeg could not convert speech to MP3: " + Describe(result));
        }
        finally
        {
            if (File.Exists(wavPath))
                File.Delete(wavPath);
        }
    }

    private async Task SynthesizeEdgeAsync(
        string text,
        string voiceId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(
            ResolvePythonPath(),
            ["-m", "edge_tts", "--voice", voiceId, "--text", text, "--write-media", outputPath],
            cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException("Edge TTS failed: " + Describe(result));
    }

    private string ResolvePythonPath()
    {
        var configured = speech.PythonPath;
        if (Path.IsPathRooted(configured) || File.Exists(configured))
            return Path.GetFullPath(configured);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var executable in new[] { "python.exe", "python", "python3" })
            {
                var candidate = Path.Combine(directory.Trim('"'), executable);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException("Python was not found for Edge TTS.", configured);
    }

    private static string NormalizeProvider(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var provider = value.Trim();
        if (provider.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            return "Windows";
        if (provider.Equals("Edge", StringComparison.OrdinalIgnoreCase))
            return "Edge";

        throw new ArgumentException("Unsupported speech provider: " + value, nameof(value));
    }

    private static string NormalizeVoiceId(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var voiceId = value.Trim();
        if (voiceId.Length > 200)
            throw new ArgumentException("The voice identifier is too long.", nameof(value));

        return voiceId;
    }

    private static string GetLocale(string voiceId)
    {
        var parts = voiceId.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : string.Empty;
    }

    private static string Describe(ProcessExecutionResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        message = message.Trim();
        return message.Length <= 1000 ? message : message[..1000];
    }
}
