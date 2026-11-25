using VideoScriptAI.Infrastructure.Processing;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class ProcessRunnerTests
{
    private static string CommandProcessor =>
        Environment.GetEnvironmentVariable("ComSpec")
        ?? throw new InvalidOperationException("ComSpec is not available.");

    [Fact]
    public async Task RunAsync_captures_output_and_exit_code()
    {
        if (!OperatingSystem.IsWindows()) return;

        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            CommandProcessor,
            new[] { "/c", "echo hello && echo problem 1>&2 && exit /b 3" },
            CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
        Assert.Contains("problem", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_rejects_missing_executable()
    {
        var runner = new ProcessRunner();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            runner.RunAsync(missing, Array.Empty<string>(), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_cancellation_terminates_child_process()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var runner = new ProcessRunner();
        var started = DateTimeOffset.UtcNow;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(CommandProcessor, new[] { "/c", "ping -n 30 127.0.0.1" }, cancellation.Token));

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5),
            "Cancellation did not terminate the process promptly.");
    }
}
