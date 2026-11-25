namespace VideoScriptAI.Application.Abstractions;

public interface IJobCancellationRegistry
{
    CancellationToken GetToken(Guid jobId);
    void Cancel(Guid jobId);
    void Remove(Guid jobId);
}
