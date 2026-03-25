using System.Net;
using System.Net.Http.Json;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesControllerTests(PersonalDashboardApiApplication app) : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenFavoriteLeagueCalledThenReturns204()
    {
        const string leagueApiAlias = "777";
        const int seasonYear = 2025;
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { LeagueId = league.Id, Year = seasonYear, IsCurrent = true, League = league });
        await app.AddToDbAsync(league);
        await app.HttpHandler.SetupGetTeams(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            long.Parse(leagueApiAlias),
            seasonYear,
            []
        );

        var response = await _client.PostAsync($"/leagues/{league.Id}/favorite", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnfavoriteLeagueCalledThenReturns204()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        await app.AddToDbAsync(league);

        var response = await _client.PostAsync($"/leagues/{league.Id}/unfavorite", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoriteNonExistentLeagueThenReturns404()
    {
        var response = await _client.PostAsync($"/leagues/{Guid.NewGuid()}/favorite", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WhenGetLeaguesCalledThenReturnsSeasonsInModel()
    {
        const string leagueApiAlias = "778";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { LeagueId = league.Id, Year = 2025, IsCurrent = true, League = league });
        await app.AddToDbAsync(league);

        var response = await _client.GetAsync("/leagues");
        var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();

        var leagueModel = result?.Items.FirstOrDefault(l => l.Id == league.Id);
        Assert.NotNull(leagueModel);
        Assert.Single(leagueModel.Seasons, s => s.Year == 2025 && s.IsCurrent);
    }

    [Fact]
    public async Task WhenGetLeaguesCalledThenReturnsFavoritesFirst()
    {
        var favorite = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        var unfavorite = PersonalDashboardEntityFactory.FootballLeague();
        await app.AddToDbAsync(favorite);
        await app.AddToDbAsync(unfavorite);

        var response = await _client.GetAsync("/leagues?limit=100");
        var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();

        Assert.NotNull(result);
        var favoriteIndex = Array.FindIndex(result.Items, l => l.Id == favorite.Id);
        var unfavoriteIndex = Array.FindIndex(result.Items, l => l.Id == unfavorite.Id);
        Assert.True(favoriteIndex < unfavoriteIndex);
    }
}
