using System.Runtime.Versioning;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Infrastructure.Processing;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public class TextToSpeechServiceTests
{
    [Fact]
    public async Task SynthesizeAsync_RejectsEmptyText()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
            new SynthesizeSpeechRequest("  ", "Windows", "Microsoft David Desktop"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SynthesizeAsync_RejectsUnsupportedProvider()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
            new SynthesizeSpeechRequest("hello", "Unknown", "voice"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SynthesizeAsync_RejectsTooMuchText()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
            new SynthesizeSpeechRequest(new string('a', 100_001), "Windows", "Microsoft David Desktop"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SynthesizeAsync_EdgePassesLongTextThroughTempFile()
    {
        var runner = new EdgeStubProcessRunner();
        var service = new TextToSpeechService(
            new StorageOptions(Path.Combine(Path.GetTempPath(), "videoscriptai-tts-tests"), 1024),
            new EngineOptions
            {
                FfmpegPath = "ffmpeg.exe",
                WhisperPath = "whisper.exe",
                ModelsPath = Path.GetTempPath()
            },
            new TextToSpeechOptions("python"),
            runner);
        var text = new string('a', 20_000);

        await service.SynthesizeAsync(
            new SynthesizeSpeechRequest(text, "Edge", "en-US-AriaNeural"),
            CancellationToken.None);

        Assert.Contains("--file", runner.SynthesizeArguments!);
        Assert.DoesNotContain("--text", runner.SynthesizeArguments!);
        Assert.Equal(text, runner.TextFromFile);
    }

    [Fact]
    public async Task SynthesizeAsync_BuildsFriendlyFileNameFromText()
    {
        var service = new TextToSpeechService(
            new StorageOptions(Path.Combine(Path.GetTempPath(), "videoscriptai-tts-tests"), 1024),
            new EngineOptions
            {
                FfmpegPath = "ffmpeg.exe",
                WhisperPath = "whisper.exe",
                ModelsPath = Path.GetTempPath()
            },
            new TextToSpeechOptions("python"),
            new EdgeStubProcessRunner());

        var result = await service.SynthesizeAsync(
            new SynthesizeSpeechRequest("Hola mundo, ¿cómo estás?\nNecesito audio.", "Edge", "en-US-AriaNeural"),
            CancellationToken.None);

        Assert.StartsWith("hola_mundo_como_", result.FileName);
        Assert.EndsWith(".mp3", result.FileName);
    }

    [Fact]
    public void ParseEdgeVoiceList_ReadsVoicesAndLocales()
    {
        const string output = """
            Name                              Gender    ContentCategories
            --------------------------------  --------  ---------------------
            en-US-AriaNeural                  Female    News, Novel
            es-MX-DaliaNeural                 Female    General
            """;

        var voices = TextToSpeechService.ParseEdgeVoiceList(output);

        Assert.Equal(2, voices.Count);
        Assert.Equal("Edge", voices[0].Provider);
        Assert.Equal("en-US-AriaNeural", voices[0].Id);
        Assert.Equal("en-US", voices[0].Locale);
        Assert.Equal("Female", voices[0].Gender);
        Assert.Equal("es-MX", voices[1].Locale);
    }

    private static TextToSpeechService CreateService() => new(
        new StorageOptions(Path.Combine(Path.GetTempPath(), "videoscriptai-tts-tests"), 1024),
        new EngineOptions
        {
            FfmpegPath = "ffmpeg.exe",
            WhisperPath = "whisper.exe",
            ModelsPath = Path.GetTempPath()
        },
        new TextToSpeechOptions("python"),
        new StubProcessRunner());

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessExecutionResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessExecutionResult(0, string.Empty, string.Empty));
    }

    private sealed class EdgeStubProcessRunner : IProcessRunner
    {
        public IReadOnlyList<string>? SynthesizeArguments { get; private set; }
        public string? TextFromFile { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            if (arguments.Contains("--list-voices"))
                return Task.FromResult(new ProcessExecutionResult(0, "en-US-AriaNeural Female News\n", string.Empty));

            SynthesizeArguments = arguments;
            var textIndex = Array.IndexOf(arguments.ToArray(), "--file");
            TextFromFile = File.ReadAllText(arguments[textIndex + 1]);

            var mediaIndex = Array.IndexOf(arguments.ToArray(), "--write-media");
            File.WriteAllText(arguments[mediaIndex + 1], "mp3");

            return Task.FromResult(new ProcessExecutionResult(0, string.Empty, string.Empty));
        }
    }
}
