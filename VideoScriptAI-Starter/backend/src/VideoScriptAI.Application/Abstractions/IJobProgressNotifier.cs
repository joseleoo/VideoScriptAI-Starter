using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.Application.Abstractions;

public interface IJobProgressNotifier
{
    Task PublishAsync(JobProgressNotification notification, CancellationToken cancellationToken);
}
