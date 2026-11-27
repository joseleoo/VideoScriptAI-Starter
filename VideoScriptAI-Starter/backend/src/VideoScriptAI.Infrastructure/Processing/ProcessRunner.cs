using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Executable not found.", executablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start process: " + executablePath);

        var standardOutputTask = ReadAndReportAsync(process.StandardOutput, output, cancellationToken);
        var standardErrorTask = ReadAndReportAsync(process.StandardError, output, cancellationToken);

        using var registration = cancellationToken.Register(() => KillProcessTree(process));
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            return new ProcessExecutionResult(process.ExitCode, standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            try
            {
                await Task.WhenAll(standardOutputTask, standardErrorTask);
            }
            catch
            {
            }

            throw;
        }
    }

    private static async Task<string> ReadAndReportAsync(
        StreamReader reader,
        IProgress<string>? output,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            builder.AppendLine(line);
            output?.Report(line);
        }

        return builder.ToString();
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}
