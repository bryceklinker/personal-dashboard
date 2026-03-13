using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class RefreshAllFavoritedClubsCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private const int SeasonYear = 2025;
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshAllFavoritedClubsCommandTests()
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
    public async Task WhenFavoritedLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommand()
    {
        const string leagueApiAlias = "500";
        var league = PersonalDashboardEntityFactory.FootballLeague(l =>
        {
            l.Favorite();
            l.AddAlias(DataSource.FootballApi, leagueApiAlias);
        });
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, []);

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == league.Id && c.SeasonYear == SeasonYear);
    }

    [Fact]
    public async Task WhenFavoritedLeagueHasNoCurrentSeasonThenSkipsIt()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }

    [Fact]
    public async Task WhenLeagueIsNotFavoritedThenSkipsIt()
    {
        const string leagueApiAlias = "501";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }
}
