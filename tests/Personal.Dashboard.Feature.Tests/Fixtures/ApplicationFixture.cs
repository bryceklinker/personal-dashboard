[CollectionDefinition("Application")]
public class ApplicationCollection : ICollectionFixture<ApplicationFixture> { }

public class ApplicationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Personal_Dashboard_Host>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
    }

    public string GetEndpoint(string resourceName)
    {
        return _app?.GetEndpoint(resourceName).ToString()
            ?? throw new InvalidOperationException("Application not started");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
