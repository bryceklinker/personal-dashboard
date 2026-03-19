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

        var resolvedCountries = UpsertCountries(response.Response, existingCountryAliases);

        foreach (var apiLeague in response.Response)
        {
            var country = resolvedCountries[apiLeague.Country.Name.ToLowerInvariant()];
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

    private Dictionary<string, FootballCountryEntity> UpsertCountries(
        FootballApiLeague[] apiLeagues,
        Dictionary<string, FootballCountryAlias> existingCountryAliases)
    {
        var resolved = new Dictionary<string, FootballCountryEntity>();
        foreach (var apiCountry in apiLeagues
            .Select(l => l.Country)
            .DistinctBy(c => c.Name.ToLowerInvariant()))
        {
            var key = apiCountry.Name.ToLowerInvariant();
            if (existingCountryAliases.TryGetValue(key, out var existingAlias))
            {
                existingAlias.Country.UpdateFromFootballApi(apiCountry);
                resolved[key] = existingAlias.Country;
            }
            else
            {
                var country = FootballCountryEntity.CreateFromFootballApi(apiCountry);
                context.Add(country);
                resolved[key] = country;
            }
        }
        return resolved;
    }
}
