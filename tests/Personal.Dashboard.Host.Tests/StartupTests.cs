using Personal.Dashboard.Host.Tests.Support;

namespace Personal.Dashboard.Host.Tests;

public class StartupTests : IAsyncLifetime
{
    private PersonalDashboardApplication? _app;
    
    public async Task InitializeAsync()
    {
        _app = await PersonalDashboardApplication.StartAsync();
    }

    [Fact]
    public async Task WhenApplicationStartedThenApiIsHealthy()
    {
        var client = _app.CreateHttpClient(PersonalDashboardServiceName.Api);
        
        var response = await client.GetAsync("/.health");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenApplicationStartedThenWebIsAvailable()
    {
        var client = _app.CreateHttpClient(PersonalDashboardServiceName.Web);
        
        var response = await client.GetAsync("/");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }
}
