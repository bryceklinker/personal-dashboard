using Personal.Dashboard.Api.Host.Common.SignalR;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Clubs;

public class ClubsRefreshedEventHandler(ISignalRPublisher publisher)
    : IEventHandler<ClubsRefreshedEvent>
{
    public async Task Handle(ClubsRefreshedEvent notification, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(new ClubsRefreshedDashboardEvent(), cancellationToken)
            .ConfigureAwait(false);
    }
}
