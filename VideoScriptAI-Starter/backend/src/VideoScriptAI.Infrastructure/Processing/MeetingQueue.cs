using System.Threading.Channels;
using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.Infrastructure.Processing;

public sealed class MeetingQueue : IMeetingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(Guid meetingId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(meetingId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
