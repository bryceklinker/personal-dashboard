using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record RefreshAllFavoritedClubsCommand : ICommand;

public class RefreshAllFavoritedClubsCommandHandler(
    PersonalDashboardContext db,
    ICqrsBus bus
) : ICommandHandler<RefreshAllFavoritedClubsCommand>
{
    public async Task Handle(RefreshAllFavoritedClubsCommand request, CancellationToken cancellationToken)
    {
        var leagues = await db.Set<FootballLeagueEntity>()
            .Where(l => l.IsFavorite && l.Seasons.Any(s => s.IsCurrent))
            .Include(l => l.Seasons)
            .ToListAsync(cancellationToken);

        foreach (var league in leagues)
        {
            var current = league.Seasons.First(s => s.IsCurrent);
            await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, current.Year), cancellationToken);
        }
    }
}
