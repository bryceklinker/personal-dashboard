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
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        foreach (var apiLeague in response.Response)
        {
            var aliasKey = $"{apiLeague.League.Id}";
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.League.UpdateFromFootballApi(apiLeague);
            }
            else
            {
                var entity = new FootballLeagueEntity();
                entity.AddAlias(DataSource.FootballApi, aliasKey);
                entity.UpdateFromFootballApi(apiLeague);
                context.Add(entity);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await bus.PublishAsync(new LeaguesRefreshedEvent(), cancellationToken);
    }
}
