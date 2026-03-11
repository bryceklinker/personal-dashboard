using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record FavoriteLeagueCommand(Guid LeagueId) : ICommand;

public class FavoriteLeagueCommandHandler(
    PersonalDashboardContext db,
    ICqrsBus bus
) : ICommandHandler<FavoriteLeagueCommand>
{
    public async Task Handle(FavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>().FindAsync([request.LeagueId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);
        league.Favorite();
        await db.SaveChangesAsync(cancellationToken);
        await bus.ExecuteAsync(new RefreshClubsCommand(request.LeagueId), cancellationToken);
    }
}
