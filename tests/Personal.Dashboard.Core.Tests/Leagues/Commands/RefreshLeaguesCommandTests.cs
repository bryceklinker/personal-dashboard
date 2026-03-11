using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class RefreshLeaguesCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshLeaguesCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(
            configure: opts => opts.ConfigureFootballApi = api => api.BaseUrl = BaseUrl,
            configureServices: services => services.AddCapturingCqrsBus()
        );
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
        _cqrsBus = provider.GetRequiredService<CapturingCqrsBus>();
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenCreatesNewLeaguesInDatabase()
    {
        var league = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [league]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dbLeagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Single(dbLeagues);
        Assert.Equal(league.League.Name, dbLeagues[0].Name);
        Assert.NotNull(dbLeagues[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenUpdatesExistingLeagueInDatabase()
    {
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague();
        existingLeague.AddAlias(DataSource.FootballApi, "42");
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        var apiLeague = FootballApiDataFactory.League();
        var apiLeagueWithKnownId = apiLeague with { League = apiLeague.League with { Id = 42 } };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeagueWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dbLeagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Single(dbLeagues);
        Assert.Equal(apiLeagueWithKnownId.League.Name, dbLeagues[0].Name);
        Assert.NotNull(dbLeagues[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenPublishesLeaguesRefreshedEvent()
    {
        await _handler.SetupGetLeagues(BaseUrl, [FootballApiDataFactory.League()]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        Assert.Single(_cqrsBus.GetCapturedEvents<LeaguesRefreshedEvent>());
    }

    [Fact]
    public async Task WhenRefreshCompletedThenDispatchesRefreshClubsCommand()
    {
        await _handler.SetupGetLeagues(BaseUrl, [FootballApiDataFactory.League()]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == null);
    }
}
