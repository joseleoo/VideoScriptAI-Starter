using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Infrastructure.Processing;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class WhisperCliTranscriptionEngineTests
{
    private const string SampleJson = """
        {
          "result": { "language": "en" },
          "transcription": [
            {
              "timestamps": { "from": "00:00:00,000", "to": "00:00:01,500" },
              "offsets": { "from": 0, "to": 1500 },
              "text": " Hello world"
            },
            {
              "timestamps": { "from": "00:00:01,500", "to": "00:00:03,250" },
              "offsets": { "from": 1500, "to": 3250 },
              "text": " Second segment"
            }
          ]
        }
        """;

    [Fact]
    public async Task TranscribeAsync_builds_expected_arguments_and_parses_json()
    {
        var root = NewTempRoot();
        var audioPath = await WriteStubFileAsync(root, "audio.wav");
        var modelPath = await WriteStubFileAsync(root, "ggml-small.bin");
        var whisperPath = await WriteStubFileAsync(root, "whisper-cli.exe");

        try
        {
            var runner = new FakeProcessRunner { Json = SampleJson };
            var engine = new WhisperCliTranscriptionEngine(
                runner,
                new EngineOptions { WhisperPath = whisperPath, WhisperThreads = 6 });
            var progress = new List<int>();

            var result = await engine.TranscribeAsync(
                new TranscriptionRequest(audioPath, modelPath, "en"),
                new SynchronousProgress<int>(progress.Add),
                CancellationToken.None);

            Assert.Equal(whisperPath, runner.ExecutablePath);
            Assert.Equal("-m", runner.Arguments[0]);
            Assert.Equal(modelPath, runner.Arguments[1]);
            Assert.Equal("-f", runner.Arguments[2]);
            Assert.Equal(audioPath, runner.Arguments[3]);
            Assert.Contains("-l", runner.Arguments);
            Assert.Equal("en", runner.Arguments[Array.IndexOf(runner.Arguments.ToArray(), "-l") + 1]);
            Assert.Contains("-t", runner.Arguments);
            Assert.Equal("6", runner.Arguments[Array.IndexOf(runner.Arguments.ToArray(), "-t") + 1]);
            Assert.Contains("-oj", runner.Arguments);
            Assert.Contains("-of", runner.Arguments);
            Assert.Equal(new[] { 5, 90, 100 }, progress);
            Assert.Equal("en", result.Language);
            Assert.Equal("Hello world Second segment", result.FullText);
            Assert.Equal(2, result.Segments.Count);
            Assert.Equal(0, result.Segments[0].StartSeconds);
            Assert.Equal(1.5, result.Segments[0].EndSeconds);
            Assert.Equal(3.25, result.Segments[1].EndSeconds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TranscribeAsync_uses_auto_language_and_detects_result_language()
    {
        var root = NewTempRoot();
        var audioPath = await WriteStubFileAsync(root, "audio.wav");
        var modelPath = await WriteStubFileAsync(root, "ggml-small.bin");
        var whisperPath = await WriteStubFileAsync(root, "whisper-cli.exe");
        var detectedJson = SampleJson.Replace("\"en\"", "\"es\"");

        try
        {
            var runner = new FakeProcessRunner { Json = detectedJson };
            var engine = new WhisperCliTranscriptionEngine(
                runner,
                new EngineOptions { WhisperPath = whisperPath });

            var result = await engine.TranscribeAsync(
                new TranscriptionRequest(audioPath, modelPath, "auto"),
                null,
                CancellationToken.None);

            Assert.Equal("auto", runner.Arguments[Array.IndexOf(runner.Arguments.ToArray(), "-l") + 1]);
            Assert.Equal("es", result.Language);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TranscribeAsync_rejects_failed_process()
    {
        var root = NewTempRoot();
        var audioPath = await WriteStubFileAsync(root, "audio.wav");
        var modelPath = await WriteStubFileAsync(root, "ggml-small.bin");
        var whisperPath = await WriteStubFileAsync(root, "whisper-cli.exe");

        try
        {
            var runner = new FakeProcessRunner
            {
                Result = new ProcessExecutionResult(2, string.Empty, "decode error")
            };
            var engine = new WhisperCliTranscriptionEngine(
                runner,
                new EngineOptions { WhisperPath = whisperPath });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.TranscribeAsync(
                    new TranscriptionRequest(audioPath, modelPath, "en"),
                    null,
                    CancellationToken.None));

            Assert.Contains("decode error", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TranscribeAsync_rejects_missing_model()
    {
        var root = NewTempRoot();
        var audioPath = await WriteStubFileAsync(root, "audio.wav");
        var whisperPath = await WriteStubFileAsync(root, "whisper-cli.exe");

        try
        {
            var runner = new FakeProcessRunner();
            var engine = new WhisperCliTranscriptionEngine(
                runner,
                new EngineOptions { WhisperPath = whisperPath });

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                engine.TranscribeAsync(
                    new TranscriptionRequest(audioPath, Path.Combine(root, "missing.bin"), "en"),
                    null,
                    CancellationToken.None));

            Assert.Equal(string.Empty, runner.ExecutablePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "vsa-whisper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> WriteStubFileAsync(string root, string name)
    {
        var path = Path.Combine(root, name);
        await File.WriteAllTextAsync(path, "stub");
        return path;
    }

    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string ExecutablePath { get; private set; } = string.Empty;
        public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();
        public ProcessExecutionResult Result { get; set; } = new(0, string.Empty, string.Empty);
        public string Json { get; set; } = SampleJson;

        public Task<ProcessExecutionResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            ExecutablePath = executablePath;
            Arguments = arguments;
            var outputIndex = arguments.ToList().IndexOf("-of");
            if (outputIndex >= 0 && outputIndex + 1 < arguments.Count)
            {
                var jsonPath = arguments[outputIndex + 1] + ".json";
                File.WriteAllText(jsonPath, Json);
            }

            return Task.FromResult(Result);
        }
    }
}
