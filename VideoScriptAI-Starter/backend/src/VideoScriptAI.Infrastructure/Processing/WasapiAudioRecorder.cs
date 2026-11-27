using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;

namespace VideoScriptAI.Infrastructure.Processing;

[SupportedOSPlatform("windows")]
public sealed class WasapiAudioRecorder(
    IMeetingFileStorage fileStorage,
    EngineOptions engine,
    IProcessRunner runner,
    ILogger<WasapiAudioRecorder> logger) : IAudioRecorder
{
    private readonly object _sync = new();
    private readonly List<CaptureSession> _sessions = [];
    private string? _path;
    private Guid? _meetingId;
    private DateTimeOffset? _startedAt;
    private long _bytesWritten;

    public bool IsRecording
    {
        get
        {
            lock (_sync)
                return _sessions.Count > 0;
        }
    }

    public RecordingStatus GetStatus()
    {
        lock (_sync)
        {
            return new RecordingStatus(IsRecording, _meetingId, _startedAt, _bytesWritten);
        }
    }

    public Task StartAsync(Guid meetingId, RecordingCaptureSource captureSource, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_sessions.Count > 0)
                throw new InvalidOperationException("Ya hay una grabación en curso.");

            var path = fileStorage.GetRecordingPath(meetingId);
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);

            var kinds = captureSource switch
            {
                RecordingCaptureSource.System => new[] { RecordingCaptureSource.System },
                RecordingCaptureSource.Microphone => new[] { RecordingCaptureSource.Microphone },
                _ => new[] { RecordingCaptureSource.System, RecordingCaptureSource.Microphone }
            };

            var started = new List<CaptureSession>();
            try
            {
                foreach (var kind in kinds)
                {
                    var capture = kind == RecordingCaptureSource.System
                        ? new WasapiLoopbackCapture()
                        : new WasapiCapture();
                    var tempPath = Path.Combine(directory, $"{meetingId:N}_{kind.ToString().ToLowerInvariant()}.wav");
                    var session = new CaptureSession(capture, new WaveFileWriter(tempPath, capture.WaveFormat), tempPath);

                    capture.DataAvailable += (_, args) =>
                    {
                        try
                        {
                            session.Writer.Write(args.Buffer, 0, args.BytesRecorded);
                            Interlocked.Add(ref session.BytesWritten, args.BytesRecorded);
                            Interlocked.Add(ref _bytesWritten, args.BytesRecorded);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, "Error al escribir el audio de la reunión.");
                        }
                    };
                    capture.RecordingStopped += (_, args) =>
                    {
                        if (args.Exception is not null)
                            logger.LogError(args.Exception, "Error al capturar el audio de la reunión.");
                        session.Stopped.TrySetResult(true);
                    };

                    started.Add(session);
                }

                foreach (var session in started)
                    session.Capture.StartRecording();

                _sessions.AddRange(started);
                _path = path;
                _meetingId = meetingId;
                _startedAt = DateTimeOffset.UtcNow;
                _bytesWritten = 0;
            }
            catch
            {
                foreach (var session in started)
                {
                    try
                    {
                        session.Capture.StopRecording();
                    }
                    catch
                    {
                    }

                    session.Writer.Dispose();
                    session.Capture.Dispose();
                    DeleteFile(session.Path);
                }

                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken)
    {
        List<CaptureSession> sessions;
        string path;

        lock (_sync)
        {
            if (_sessions.Count == 0 || _path is null)
                throw new InvalidOperationException("No hay una grabación en curso.");

            sessions = [.. _sessions];
            path = _path;
        }

        try
        {
            foreach (var session in sessions)
            {
                try
                {
                    session.Capture.StopRecording();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "No se pudo detener una fuente de audio limpiamente.");
                    session.Stopped.TrySetResult(true);
                }
            }

            foreach (var session in sessions)
            {
                await session.Stopped.Task.WaitAsync(cancellationToken);
                session.Writer.Flush();
                session.Writer.Dispose();
                session.Capture.Dispose();
            }

            if (sessions.Sum(session => session.BytesWritten) == 0)
                throw new InvalidOperationException("No se capturó audio. Comprueba el dispositivo seleccionado y vuelve a intentarlo.");

            if (sessions.Count == 1)
                File.Move(sessions[0].Path, path, overwrite: true);
            else
                await MixAsync(sessions, path, cancellationToken);

            return path;
        }
        finally
        {
            foreach (var session in sessions)
                DeleteFile(session.Path);

            lock (_sync)
            {
                _sessions.Clear();
                _path = null;
                _meetingId = null;
                _startedAt = null;
                _bytesWritten = 0;
            }
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        string? path;
        lock (_sync)
            path = _path;

        if (IsRecording)
        {
            try
            {
                await StopAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "No se pudo detener limpiamente la grabación cancelada.");
            }
        }

        if (path is not null)
            DeleteFile(path);
    }

    private async Task MixAsync(
        IReadOnlyList<CaptureSession> sessions,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(engine.FfmpegPath) || !File.Exists(engine.FfmpegPath))
            throw new FileNotFoundException("FFmpeg executable not found.", engine.FfmpegPath);

        var labels = string.Concat(Enumerable.Range(0, sessions.Count).Select(index => $"[{index}:a]"));
        var arguments = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y"
        };
        foreach (var session in sessions)
        {
            arguments.Add("-i");
            arguments.Add(session.Path);
        }
        arguments.AddRange(new[]
        {
            "-filter_complex", $"{labels}amix=inputs={sessions.Count}:duration=longest:dropout_transition=0[a]",
            "-map", "[a]", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", outputPath
        });

        var result = await runner.RunAsync(engine.FfmpegPath, arguments.ToArray(), cancellationToken);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException("No se pudo mezclar el audio capturado: " + result.StandardError.Trim());
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class CaptureSession(IWaveIn capture, WaveFileWriter writer, string path)
    {
        public IWaveIn Capture { get; } = capture;
        public WaveFileWriter Writer { get; } = writer;
        public string Path { get; } = path;
        public long BytesWritten;
        public TaskCompletionSource<bool> Stopped { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
