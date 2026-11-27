namespace VideoScriptAI.Application.Abstractions;

public enum RecordingCaptureSource
{
    System,
    Microphone,
    Both
}

public sealed record RecordingStatus(
    bool IsRecording,
    Guid? MeetingId,
    DateTimeOffset? StartedAtUtc,
    long BytesWritten);

public interface IAudioRecorder
{
    bool IsRecording { get; }
    RecordingStatus GetStatus();
    Task StartAsync(Guid meetingId, RecordingCaptureSource captureSource, CancellationToken cancellationToken);
    Task<string> StopAsync(CancellationToken cancellationToken);
    Task CancelAsync(CancellationToken cancellationToken);
}
