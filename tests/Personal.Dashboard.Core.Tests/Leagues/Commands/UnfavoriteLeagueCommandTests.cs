using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class UnfavoriteLeagueCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public UnfavoriteLeagueCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenLeagueExistsThenSetsIsFavoriteToFalse()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new UnfavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.False(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new UnfavoriteLeagueCommand(Guid.NewGuid())));
    }
}
