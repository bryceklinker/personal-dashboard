using System.Net;
using System.Net.Http.Json;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Tests.Clubs;

public class ClubsControllerTests(PersonalDashboardApiApplication app) : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenGetClubsCalledThenReturnsClubs()
    {
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballClub());
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballClub());

        var response = await _client.GetAsync("/clubs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballClubModel>>();
        Assert.NotNull(result);
        Assert.True(result.Total >= 2);
    }

    [Fact]
    public async Task WhenRefreshClubsCalledThenReturns204()
    {
        var response = await _client.PostAsync("/clubs/refresh", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoriteClubCalledThenReturns204()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        await app.AddToDbAsync(club);

        var response = await _client.PostAsync($"/clubs/{club.Id}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnfavoriteClubCalledThenReturns204()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        await app.AddToDbAsync(club);

        var response = await _client.PostAsync($"/clubs/{club.Id}/unfavorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoriteNonExistentClubThenReturns404()
    {
        var response = await _client.PostAsync($"/clubs/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
