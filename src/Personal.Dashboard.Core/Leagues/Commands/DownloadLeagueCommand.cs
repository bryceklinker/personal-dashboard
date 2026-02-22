using MediatR;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record DownloadLeagueCommand(string Name) : ICommand;

public class DownloadLeagueCommandHandler(
    IFootballApiClient client,
    PersonalDashboardContext context
) : IRequestHandler<DownloadLeagueCommand>
{
    public async Task Handle(DownloadLeagueCommand request, CancellationToken cancellationToken)
    {
        var parameters = new FootballApiLeaguesParameters(Name: request.Name);
        var response = await client.GetLeaguesAsync(parameters);
        var entities = response.Response.Select(ToEntity);
        context.Set<FootballLeagueEntity>().AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static FootballLeagueEntity ToEntity(FootballApiLeague league)
    {
        var entity = new FootballLeagueEntity
        {
            Name = league.League.Name
        };
        entity.AddAlias(DataSource.FootballApi, $"{league.League.Id}");
        return entity;
    }
}