namespace VideoScriptAI.Application.Abstractions;

public sealed record RecordingStatus(
    bool IsRecording,
    Guid? MeetingId,
    DateTimeOffset? StartedAtUtc,
    long BytesWritten);

public interface IAudioRecorder
{
    bool IsRecording { get; }
    RecordingStatus GetStatus();
    Task StartAsync(Guid meetingId, CancellationToken cancellationToken);
    Task<string> StopAsync(CancellationToken cancellationToken);
    Task CancelAsync(CancellationToken cancellationToken);
}
