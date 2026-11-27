using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IMeetingProgressNotifier
{
    Task PublishAsync(MeetingProgressNotification notification, CancellationToken cancellationToken);
}
