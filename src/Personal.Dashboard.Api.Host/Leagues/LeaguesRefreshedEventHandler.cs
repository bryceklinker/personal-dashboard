using Personal.Dashboard.Api.Host.Common.SignalR;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Leagues;

public class LeaguesRefreshedEventHandler(ISignalRPublisher publisher)
    : IEventHandler<LeaguesRefreshedEvent>
{
    public async Task Handle(LeaguesRefreshedEvent notification, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(new LeaguesRefreshedDashboardEvent(), cancellationToken)
            .ConfigureAwait(false);
    }
}
