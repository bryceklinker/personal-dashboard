using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    ICqrsBus bus,
    ILogger<FavoriteLeagueCommandHandler> logger
) : ICommandHandler<FavoriteLeagueCommand>
{
    public async Task Handle(FavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>()
            .Include(l => l.Seasons)
            .FirstOrDefaultAsync(l => l.Id == request.LeagueId, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);

        league.Favorite();
        await db.SaveChangesAsync(cancellationToken);

        var currentSeason = league.Seasons.FirstOrDefault(s => s.IsCurrent);
        if (currentSeason is null)
        {
            logger.LogWarning("League {LeagueId} has no current season; clubs not loaded", request.LeagueId);
            return;
        }

        try
        {
            await bus.ExecuteAsync(new RefreshClubsCommand(request.LeagueId, currentSeason.Year), cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to refresh clubs for league {LeagueId}", request.LeagueId);
        }
    }
}
