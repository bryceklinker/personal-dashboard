using System.Net;
using Personal.Dashboard.Api.Host.Tests.Support;

namespace Personal.Dashboard.Api.Host.Tests.Health;

public class HealthApiTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    [Fact]
    public async Task WhenGettingHealthThenReturnsHealthy()
    {
        var client = app.CreateClient();
        
        var response = await client.GetAsync("/.health");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}