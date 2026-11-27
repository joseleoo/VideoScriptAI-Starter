using Microsoft.AspNetCore.SignalR;
using VideoScriptAI.API.Hubs;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.API.Notifications;

public sealed class SignalRMeetingProgressNotifier(IHubContext<JobsHub> hubContext) : IMeetingProgressNotifier
{
    public Task PublishAsync(MeetingProgressNotification notification, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("meetingProgress", notification, cancellationToken);
}
