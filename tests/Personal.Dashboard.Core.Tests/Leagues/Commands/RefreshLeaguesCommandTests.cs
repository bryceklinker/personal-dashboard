using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Core.Tests.Support;
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
    public async Task WhenRefreshingLeaguesThenUpsertsSeasonsForEachLeague()
    {
        var apiSeason = FootballApiDataFactory.Season(current: true);
        var apiLeague = FootballApiDataFactory.League() with { Seasons = [apiSeason] };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var seasons = await _context.Set<FootballLeagueSeason>().ToArrayAsync();
        Assert.Single(seasons);
        Assert.Equal((int)apiSeason.Year, seasons[0].Year);
        Assert.True(seasons[0].IsCurrent);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenUpdatesExistingSeasonIsCurrent()
    {
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague();
        existingLeague.AddAlias(DataSource.FootballApi, "43");
        existingLeague.Seasons.Add(new FootballLeagueSeason { Year = 2024, IsCurrent = true, League = existingLeague });
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        // API now says 2025 is current and 2024 is not
        var apiLeague = FootballApiDataFactory.League() with
        {
            League = FootballApiDataFactory.LeagueInfo() with { Id = 43 },
            Seasons =
            [
                FootballApiDataFactory.Season(current: false) with { Year = 2024 },
                FootballApiDataFactory.Season(current: true) with { Year = 2025 }
            ]
        };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var seasons = await _context.Set<FootballLeagueSeason>()
            .OrderBy(s => s.Year)
            .ToArrayAsync();
        Assert.Equal(2, seasons.Length);
        Assert.False(seasons[0].IsCurrent); // 2024
        Assert.True(seasons[1].IsCurrent);  // 2025
    }

    [Fact]
    public async Task WhenFavoritedLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommand()
    {
        const string leagueApiAlias = "44";
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague(l =>
        {
            l.Favorite();
            l.AddAlias(DataSource.FootballApi, leagueApiAlias);
        });
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        var apiSeason = FootballApiDataFactory.Season(current: true) with { Year = 2025 };
        var apiLeague = FootballApiDataFactory.League() with
        {
            League = FootballApiDataFactory.LeagueInfo() with { Id = 44 },
            Seasons = [apiSeason]
        };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), 2025, []);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == existingLeague.Id && c.SeasonYear == 2025);
    }

    [Fact]
    public async Task WhenLeagueIsNotFavoritedThenDoesNotDispatchRefreshClubsCommand()
    {
        var apiLeague = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }
}
