using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Countries.Entities;
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

        var existingCountryAliases = await context.Set<FootballCountryAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Country)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        foreach (var apiLeague in response.Response)
        {
            var countryKey = apiLeague.Country.Name.ToLowerInvariant();
            FootballCountryEntity country;
            if (existingCountryAliases.TryGetValue(countryKey, out var countryAlias))
            {
                country = countryAlias.Country;
                country.UpdateFromFootballApi(apiLeague.Country);
            }
            else
            {
                country = FootballCountryEntity.CreateFromFootballApi(apiLeague.Country);
                context.Add(country);
                existingCountryAliases[countryKey] = country.Aliases
                    .First(a => a.AliasSource == DataSource.FootballApi);
            }

            var aliasKey = $"{apiLeague.League.Id}";
            if (existingAliases.TryGetValue(aliasKey, out var alias))
                alias.League.UpdateFromFootballApi(apiLeague, country);
            else
                context.Add(FootballLeagueEntity.CreateFromFootballApi(apiLeague, country));
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
}
