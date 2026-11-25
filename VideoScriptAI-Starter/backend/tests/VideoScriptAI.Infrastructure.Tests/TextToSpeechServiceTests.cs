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
            new SynthesizeSpeechRequest(new string('a', 10_001), "Windows", "Microsoft David Desktop"),
            CancellationToken.None));
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
}
