using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class RefreshClubsCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshClubsCommandTests()
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
    public async Task WhenLeagueExistsThenRefreshesClubsForThatLeague()
    {
        var leagueApiAlias = "100";
        var clubApiAlias = "200";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(LeagueId: league.Id));

        var dbClub = await _context.Set<FootballClubEntity>().FirstOrDefaultAsync(c => c.Id == club.Id);
        Assert.NotNull(dbClub?.LastRefreshed);
        Assert.Equal(apiTeamWithKnownId.Team.Name, dbClub?.Name);
    }

    [Fact]
    public async Task WhenNewClubReturnedFromApiThenCreatesClub()
    {
        var leagueApiAlias = "101";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), [apiTeam]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(LeagueId: league.Id));

        var dbClubs = await _context.Set<FootballClubEntity>().ToArrayAsync();
        Assert.Single(dbClubs);
        Assert.Equal(apiTeam.Team.Name, dbClubs[0].Name);
        Assert.NotNull(dbClubs[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new RefreshClubsCommand(LeagueId: nonExistentId)));
    }

    [Fact]
    public async Task WhenRefreshCompletedWithLeagueIdThenPublishesClubsRefreshedEvent()
    {
        var leagueApiAlias = "102";
        var clubApiAlias = "202";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(LeagueId: league.Id));

        Assert.Single(_cqrsBus.GetCapturedEvents<ClubsRefreshedEvent>());
    }

    [Fact]
    public async Task WhenNoClubsExistThenIsNoOp()
    {
        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        var dbClubs = await _context.Set<FootballClubEntity>().ToArrayAsync();
        Assert.Empty(dbClubs);
    }

    [Fact]
    public async Task WhenClubsExistThenRefreshesAllClubs()
    {
        var leagueApiAlias = "103";
        var clubApiAlias = "203";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        var dbClub = await _context.Set<FootballClubEntity>().FirstOrDefaultAsync(c => c.Id == club.Id);
        Assert.NotNull(dbClub?.LastRefreshed);
        Assert.Equal(apiTeamWithKnownId.Team.Name, dbClub?.Name);
    }

    [Fact]
    public async Task WhenRefreshCompletedWithoutLeagueIdThenPublishesClubsRefreshedEvent()
    {
        var leagueApiAlias = "104";
        var clubApiAlias = "204";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        Assert.Single(_cqrsBus.GetCapturedEvents<ClubsRefreshedEvent>());
    }
}
