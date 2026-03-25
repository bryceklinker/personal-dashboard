using System.Net;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Api.Host.Tests.Common;

public class CqrsControllerNotFoundTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenFavoringNonExistentLeagueThenReturnsNotFound()
    {
        var response = await _client.PostAsync($"/leagues/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
