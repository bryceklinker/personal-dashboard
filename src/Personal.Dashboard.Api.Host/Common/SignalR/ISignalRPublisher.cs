using Microsoft.AspNetCore.SignalR;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Common.SignalR;

public interface ISignalRPublisher
{
    Task PublishAsync(DashboardEvent @event, CancellationToken ct = default);
}

public class SignalRPublisher(IHubContext<EventsHub> hubContext) : ISignalRPublisher
{
    public async Task PublishAsync(DashboardEvent @event, CancellationToken ct = default)
    {
        await hubContext.Clients.All.SendAsync("ReceiveEvent", @event, ct).ConfigureAwait(false);
    }
}
