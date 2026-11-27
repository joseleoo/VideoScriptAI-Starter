using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Infrastructure.Processing;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class FfmpegAudioExtractorTests
{
    [Fact]
    public async Task ExtractWavAsync_builds_expected_arguments_and_returns_output_path()
    {
        var root = NewTempRoot();
        var videoPath = Path.Combine(root, "videos", "sample input.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);
        await File.WriteAllTextAsync(videoPath, "stub");
        var ffmpegPath = await WriteStubExecutableAsync(root);

        try
        {
            var runner = new FakeProcessRunner();
            var extractor = new FfmpegAudioExtractor(runner, new EngineOptions { FfmpegPath = ffmpegPath });

            var outputPath = await extractor.ExtractWavAsync(
                videoPath, Path.Combine(root, "audio"), CancellationToken.None);

            Assert.Equal(ffmpegPath, runner.ExecutablePath);
            Assert.Equal(Path.Combine(root, "audio", "sample input.wav"), outputPath);
            Assert.Contains("-i", runner.Arguments);
            Assert.Contains(videoPath, runner.Arguments);
            Assert.Contains("-vn", runner.Arguments);
            Assert.Contains("-ac", runner.Arguments);
            Assert.Contains("1", runner.Arguments);
            Assert.Contains("-ar", runner.Arguments);
            Assert.Contains("16000", runner.Arguments);
            Assert.Contains("-c:a", runner.Arguments);
            Assert.Contains("pcm_s16le", runner.Arguments);
            Assert.Equal(outputPath, runner.Arguments[^1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".m4a")]
    [InlineData(".wav")]
    [InlineData(".flac")]
    public async Task ExtractWavAsync_accepts_audio_files(string extension)
    {
        var root = NewTempRoot();
        var audioPath = Path.Combine(root, "sample" + extension);
        await File.WriteAllTextAsync(audioPath, "stub");
        var ffmpegPath = await WriteStubExecutableAsync(root);

        try
        {
            var runner = new FakeProcessRunner();
            var extractor = new FfmpegAudioExtractor(runner, new EngineOptions { FfmpegPath = ffmpegPath });

            var outputPath = await extractor.ExtractWavAsync(
                audioPath, Path.Combine(root, "audio"), CancellationToken.None);

            Assert.Equal(ffmpegPath, runner.ExecutablePath);
            Assert.Equal(Path.Combine(root, "audio", "sample.wav"), outputPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractWavAsync_rejects_failed_process()
    {
        var root = NewTempRoot();
        var videoPath = Path.Combine(root, "sample.mp4");
        await File.WriteAllTextAsync(videoPath, "stub");
        var ffmpegPath = await WriteStubExecutableAsync(root);

        try
        {
            var runner = new FakeProcessRunner
            {
                CreateOutputFile = false,
                Result = new ProcessExecutionResult(1, string.Empty, "codec error")
            };
            var extractor = new FfmpegAudioExtractor(runner, new EngineOptions { FfmpegPath = ffmpegPath });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                extractor.ExtractWavAsync(videoPath, Path.Combine(root, "audio"), CancellationToken.None));

            Assert.Contains("codec error", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractWavAsync_rejects_unsupported_extension()
    {
        var root = NewTempRoot();
        var videoPath = Path.Combine(root, "sample.txt");
        await File.WriteAllTextAsync(videoPath, "stub");
        var ffmpegPath = await WriteStubExecutableAsync(root);
        var runner = new FakeProcessRunner();
        var extractor = new FfmpegAudioExtractor(runner, new EngineOptions { FfmpegPath = ffmpegPath });

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                extractor.ExtractWavAsync(videoPath, Path.Combine(root, "audio"), CancellationToken.None));

            Assert.Equal(string.Empty, runner.ExecutablePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "vsa-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> WriteStubExecutableAsync(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "ffmpeg.exe");
        await File.WriteAllTextAsync(path, "stub");
        return path;
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string ExecutablePath { get; private set; } = string.Empty;
        public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();
        public ProcessExecutionResult Result { get; set; } = new(0, string.Empty, string.Empty);
        public bool CreateOutputFile { get; set; } = true;

        public Task<ProcessExecutionResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            ExecutablePath = executablePath;
            Arguments = arguments;
            if (CreateOutputFile && arguments.Count > 0)
            {
                var path = arguments[^1];
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, new string('x', 256));
            }

            return Task.FromResult(Result);
        }
    }
}
