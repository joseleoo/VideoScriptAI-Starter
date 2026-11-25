using Microsoft.AspNetCore.SignalR;
using VideoScriptAI.API.Hubs;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.API.Notifications;

public sealed class SignalRJobProgressNotifier(IHubContext<JobsHub> hubContext) : IJobProgressNotifier
{
    public Task PublishAsync(JobProgressNotification notification, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("jobProgress", notification, cancellationToken);
}
