using System.Collections.Concurrent;
using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class JobCancellationRegistry : IJobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public CancellationToken GetToken(Guid jobId) =>
        _tokens.GetOrAdd(jobId, _ => new CancellationTokenSource()).Token;

    public void Cancel(Guid jobId)
    {
        if (_tokens.TryGetValue(jobId, out var source))
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public void Remove(Guid jobId)
    {
        if (_tokens.TryRemove(jobId, out var source))
            source.Dispose();
    }
}
