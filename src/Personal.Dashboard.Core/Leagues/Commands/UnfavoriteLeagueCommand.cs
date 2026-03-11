using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record UnfavoriteLeagueCommand(Guid LeagueId) : ICommand;

public class UnfavoriteLeagueCommandHandler(
    PersonalDashboardContext db
) : ICommandHandler<UnfavoriteLeagueCommand>
{
    public async Task Handle(UnfavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>().FindAsync([request.LeagueId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);
        league.Unfavorite();
        await db.SaveChangesAsync(cancellationToken);
    }
}
