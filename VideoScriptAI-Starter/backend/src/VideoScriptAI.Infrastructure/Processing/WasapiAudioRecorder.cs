using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;

namespace VideoScriptAI.Infrastructure.Processing;

[SupportedOSPlatform("windows")]
public sealed class WasapiAudioRecorder(
    IMeetingFileStorage fileStorage,
    ILogger<WasapiAudioRecorder> logger) : IAudioRecorder
{
    private readonly object _sync = new();
    private WasapiLoopbackCapture? _capture;
    private WaveFileWriter? _writer;
    private TaskCompletionSource<bool>? _stopped;
    private string? _path;
    private Guid? _meetingId;
    private DateTimeOffset? _startedAt;
    private long _bytesWritten;

    public bool IsRecording
    {
        get
        {
            lock (_sync)
                return _capture is not null;
        }
    }

    public RecordingStatus GetStatus()
    {
        lock (_sync)
        {
            return new RecordingStatus(IsRecording, _meetingId, _startedAt, _bytesWritten);
        }
    }

    public Task StartAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_capture is not null)
                throw new InvalidOperationException("Ya hay una grabación en curso.");

            var path = fileStorage.GetRecordingPath(meetingId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var capture = new WasapiLoopbackCapture();
            var writer = new WaveFileWriter(path, capture.WaveFormat);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            capture.DataAvailable += (_, args) =>
            {
                try
                {
                    writer.Write(args.Buffer, 0, args.BytesRecorded);
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
                    logger.LogError(args.Exception, "Error al capturar el audio del sistema.");
                stopped.TrySetResult(true);
            };

            _capture = capture;
            _writer = writer;
            _stopped = stopped;
            _path = path;
            _meetingId = meetingId;
            _startedAt = DateTimeOffset.UtcNow;
            _bytesWritten = 0;

            capture.StartRecording();
        }

        return Task.CompletedTask;
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken)
    {
        WasapiLoopbackCapture capture;
        WaveFileWriter writer;
        string path;

        lock (_sync)
        {
            if (_capture is null || _writer is null || _path is null || _stopped is null)
                throw new InvalidOperationException("No hay una grabación en curso.");

            capture = _capture;
            writer = _writer;
            path = _path;
        }

        capture.StopRecording();
        await _stopped.Task.WaitAsync(cancellationToken);
        writer.Flush();
        writer.Dispose();
        capture.Dispose();

        lock (_sync)
        {
            _capture = null;
            _writer = null;
            _stopped = null;
            _path = null;
            _meetingId = null;
            _startedAt = null;
        }

        return path;
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        string? path;
        lock (_sync)
        {
            path = _path;
        }

        if (_capture is not null)
        {
            try
            {
                await StopAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "No se pudo detener limpiamente la grabación cancelada.");
                lock (_sync)
                {
                    _capture?.Dispose();
                    _writer?.Dispose();
                    _capture = null;
                    _writer = null;
                    _stopped = null;
                    _path = null;
                    _meetingId = null;
                    _startedAt = null;
                }
            }
        }

        if (path is not null && File.Exists(path))
            File.Delete(path);
    }
}
