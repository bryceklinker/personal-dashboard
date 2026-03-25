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

public record RefreshClubsCommand(Guid LeagueId, int SeasonYear) : ICommand;

public class RefreshClubsCommandHandler(
    PersonalDashboardContext db,
    IFootballApiClient footballApiClient,
    ICqrsBus bus
) : ICommandHandler<RefreshClubsCommand>
{
    public async Task Handle(RefreshClubsCommand request, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>()
            .Include(l => l.Aliases)
            .FirstOrDefaultAsync(l => l.Id == request.LeagueId, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);

        var faAlias = league.Aliases.FirstOrDefault(a => a.AliasSource == DataSource.FootballApi)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueAlias), request.LeagueId);

        var existingAliases = await db.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Club)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        var response = await footballApiClient.GetTeamsAsync(
            new FootballApiTeamsParameters(League: long.Parse(faAlias.Alias), Season: request.SeasonYear));

        ProcessTeams(response.Response, league, existingAliases, cancellationToken);

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
