using System.Net;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesControllerTests(PersonalDashboardApiApplication app) : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenFavoriteLeagueCalledThenReturns204()
    {
        const string leagueApiAlias = "777";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        await app.AddToDbAsync(league);
        await app.HttpHandler.SetupGetTeams(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            long.Parse(leagueApiAlias),
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
}
