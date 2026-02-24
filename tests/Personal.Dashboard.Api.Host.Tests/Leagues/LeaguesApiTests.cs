using System.Net;
using System.Net.Http.Json;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesApiTests(PersonalDashboardApiApplication app) : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();
    
    [Fact]
    public async Task WhenGettingLeaguesThenReturnsLeaguesFromDatabase()
    {
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballLeague());
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballLeague());
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballLeague());

        var response = await _client.GetAsync("/leagues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();
        Assert.Equal(3, result?.Total);
        Assert.Equal(3, result?.Items.Length);
        Assert.Equal(0, result?.Offset);
        Assert.Equal(10, result?.Limit);
    }
}