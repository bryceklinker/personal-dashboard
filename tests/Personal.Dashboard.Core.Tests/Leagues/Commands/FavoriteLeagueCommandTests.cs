using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class FavoriteLeagueCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;
    private readonly FakeHttpMessageHandler _handler;

    public FavoriteLeagueCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(
            configure: opts => opts.ConfigureFootballApi = api => api.BaseUrl = BaseUrl,
            configureServices: services => services.AddCapturingCqrsBus()
        );
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _cqrsBus = provider.GetRequiredService<CapturingCqrsBus>();
        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
    }

    [Fact]
    public async Task WhenLeagueExistsThenSetsIsFavoriteToTrue()
    {
        const string leagueApiAlias = "300";
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), DateTimeOffset.UtcNow.Year, []);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenLeagueExistsThenDispatchesRefreshClubsCommand()
    {
        const string leagueApiAlias = "301";
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), DateTimeOffset.UtcNow.Year, []);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == league.Id);
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(Guid.NewGuid())));
    }
}
