using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class UnsupportedAudioRecorder : IAudioRecorder
{
    public bool IsRecording => false;

    public RecordingStatus GetStatus() => new(false, null, null, 0);

    public Task StartAsync(Guid meetingId, RecordingCaptureSource captureSource, CancellationToken cancellationToken) =>
        throw new PlatformNotSupportedException("La grabación en vivo requiere Windows con WASAPI.");

    public Task<string> StopAsync(CancellationToken cancellationToken) =>
        throw new PlatformNotSupportedException("La grabación en vivo requiere Windows con WASAPI.");

    public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
