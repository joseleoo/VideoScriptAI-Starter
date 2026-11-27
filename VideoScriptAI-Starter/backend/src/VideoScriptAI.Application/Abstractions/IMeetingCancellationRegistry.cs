namespace VideoScriptAI.Application.Abstractions;

public interface IMeetingCancellationRegistry
{
    CancellationToken GetToken(Guid meetingId);
    void Cancel(Guid meetingId);
    void Remove(Guid meetingId);
}
