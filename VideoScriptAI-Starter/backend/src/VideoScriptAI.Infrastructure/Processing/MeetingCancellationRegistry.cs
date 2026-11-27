using System.Collections.Concurrent;
using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class MeetingCancellationRegistry : IMeetingCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public CancellationToken GetToken(Guid meetingId) =>
        _tokens.GetOrAdd(meetingId, _ => new CancellationTokenSource()).Token;

    public void Cancel(Guid meetingId)
    {
        if (_tokens.TryGetValue(meetingId, out var source))
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

    public void Remove(Guid meetingId)
    {
        if (_tokens.TryRemove(meetingId, out var source))
            source.Dispose();
    }
}
