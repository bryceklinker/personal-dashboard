using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record RefreshClubsCommand(Guid? LeagueId = null) : ICommand;

public class RefreshClubsCommandHandler(
    PersonalDashboardContext db,
    IFootballApiClient footballApiClient,
    ICqrsBus bus
) : ICommandHandler<RefreshClubsCommand>
{
    public async Task Handle(RefreshClubsCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
        {
            await HandleWithLeague(request.LeagueId.Value, cancellationToken);
        }
        else
        {
            await HandleAllLeagues(cancellationToken);
        }
    }

    private async Task HandleWithLeague(Guid leagueId, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>()
            .Include(l => l.Aliases)
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken)
            ?? throw new EntityNotFoundException<FootballLeagueEntity>(leagueId);

        var faAlias = league.Aliases.FirstOrDefault(a => a.AliasSource == DataSource.FootballApi)
            ?? throw new EntityNotFoundException<FootballLeagueAlias>(leagueId);

        var existingAliases = await db.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Club)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        var seasonYear = league.CurrentSeasonYear ?? DateTimeOffset.UtcNow.Year - 1;
        var response = await footballApiClient.GetTeamsAsync(
            new FootballApiTeamsParameters(League: long.Parse(faAlias.Alias), Season: seasonYear));

        ProcessTeams(response.Response, league, existingAliases, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await bus.PublishAsync(new ClubsRefreshedEvent(), cancellationToken);
    }

    private async Task HandleAllLeagues(CancellationToken cancellationToken)
    {
        var clubAliases = await db.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Club)
            .ThenInclude(c => c.Leagues)
            .ThenInclude(l => l.Aliases)
            .ToListAsync(cancellationToken);

        if (clubAliases.Count == 0)
        {
            return;
        }

        var leagues = clubAliases
            .SelectMany(a => a.Club.Leagues)
            .DistinctBy(l => l.Id)
            .ToList();

        foreach (var league in leagues)
        {
            var leagueFaAlias = league.Aliases.FirstOrDefault(a => a.AliasSource == DataSource.FootballApi);
            if (leagueFaAlias is null)
            {
                continue;
            }

            var aliasDict = clubAliases
                .Where(a => a.Club.Leagues.Any(l => l.Id == league.Id))
                .ToDictionary(a => a.Alias, a => a);

            var seasonYear = league.CurrentSeasonYear ?? DateTimeOffset.UtcNow.Year - 1;
            var response = await footballApiClient.GetTeamsAsync(
                new FootballApiTeamsParameters(League: long.Parse(leagueFaAlias.Alias), Season: seasonYear));

            ProcessTeams(response.Response, league, aliasDict, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await bus.PublishAsync(new ClubsRefreshedEvent(), cancellationToken);
    }

    private void ProcessTeams(
        FootballApiTeam[] teams,
        FootballLeagueEntity league,
        Dictionary<string, FootballClubAlias> existingAliases,
        CancellationToken cancellationToken)
    {
        foreach (var team in teams)
        {
            var aliasKey = team.Team.Id.ToString();
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.Club.UpdateFromFootballApi(team);
            }
            else
            {
                var club = new FootballClubEntity();
                club.AddAlias(DataSource.FootballApi, aliasKey);
                club.AddLeague(league);
                club.UpdateFromFootballApi(team);
                db.Set<FootballClubEntity>().Add(club);
            }
        }
    }
}
