using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record RefreshLeaguesCommand : ICommand;

public class RefreshLeaguesCommandHandler(
    IFootballApiClient client,
    PersonalDashboardContext context,
    ICqrsBus bus
) : ICommandHandler<RefreshLeaguesCommand>
{
    public async Task Handle(RefreshLeaguesCommand request, CancellationToken cancellationToken)
    {
        var response = await client.GetLeaguesAsync();

        var existingAliases = await context.Set<FootballLeagueAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.League)
            .ThenInclude(l => l.Seasons)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        foreach (var apiLeague in response.Response)
        {
            var aliasKey = $"{apiLeague.League.Id}";
            FootballLeagueEntity entity;
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.League.UpdateFromFootballApi(apiLeague);
                entity = alias.League;
            }
            else
            {
                entity = new FootballLeagueEntity();
                entity.AddAlias(DataSource.FootballApi, aliasKey);
                entity.UpdateFromFootballApi(apiLeague);
                context.Add(entity);
            }

            UpsertSeasons(entity, apiLeague.Seasons);
        }

        await context.SaveChangesAsync(cancellationToken);

        var favoritedWithCurrentSeason = await context.Set<FootballLeagueEntity>()
            .Where(l => l.IsFavorite && l.Seasons.Any(s => s.IsCurrent))
            .Include(l => l.Seasons)
            .ToListAsync(cancellationToken);

        foreach (var league in favoritedWithCurrentSeason)
        {
            var currentSeason = league.Seasons.First(s => s.IsCurrent);
            await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, currentSeason.Year), cancellationToken);
        }

        await bus.PublishAsync(new LeaguesRefreshedEvent(), cancellationToken);
    }

    private void UpsertSeasons(FootballLeagueEntity entity, FootballApiSeason[] apiSeasons)
    {
        var existingSeasons = entity.Seasons.ToDictionary(s => s.Year);
        foreach (var apiSeason in apiSeasons)
        {
            var year = (int)apiSeason.Year;
            if (existingSeasons.TryGetValue(year, out var season))
                season.IsCurrent = apiSeason.Current;
            else
                context.Add(new FootballLeagueSeason { Year = year, IsCurrent = apiSeason.Current, League = entity });
        }
    }
}
