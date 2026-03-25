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
    private const int SeasonYear = 2025;
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
        var league = PersonalDashboardEntityFactory.FootballLeague();
        _context.Add(league);
        await _context.SaveChangesAsync();

        // No current season → no RefreshClubsCommand → no HTTP setup needed
        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommandWithSeasonYear()
    {
        const string leagueApiAlias = "301";
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, []);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == league.Id && c.SeasonYear == SeasonYear);
    }

    [Fact]
    public async Task WhenLeagueHasNoCurrentSeasonThenDoesNotDispatchRefreshClubsCommand()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Seasons.Add(new FootballLeagueSeason { Year = 2024, IsCurrent = false, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(Guid.NewGuid())));
    }
}
