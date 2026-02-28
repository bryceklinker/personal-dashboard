using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Personal.Dashboard.Host.Tests.Support;

public class PersonalDashboardApplication(DistributedApplication app) : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    
    public static async Task<PersonalDashboardApplication> StartAsync()
    {
        Environment.SetEnvironmentVariable("DB_HOST", "localhost");
        Environment.SetEnvironmentVariable("DB_PORT", "5432");
        Environment.SetEnvironmentVariable("DB_USER", "user");
        Environment.SetEnvironmentVariable("DB_PASSWORD", "password");
        
        var host = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Personal_Dashboard_Host>();
        host.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(host.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.*", LogLevel.Debug);
        });
        host.Services.ConfigureHttpClientDefaults(b =>
        {
            b.AddStandardResilienceHandler();
        });
        var app = await host.BuildAsync();
        await app.StartAsync().WaitAsync(DefaultTimeout);
        return new  PersonalDashboardApplication(app);
    }

    public HttpClient CreateHttpClient(string service)
    {
        PersonalDashboardServiceName.EnsureValid(service);
        return app.CreateHttpClient(service);
    }
    
    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}