using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Common.Apis;
using Personal.Dashboard.Web.Host.Common.SignalR;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public class PersonalDashboardWebContext : BunitContext
{
    public FakeHttpMessageHandler HttpHandler { get; } = new FakeHttpMessageHandler();
    public FakeHubConnectionFactory HubFactory { get; } = new FakeHubConnectionFactory();
    public FakeSnackbarService Snackbar { get; } = new FakeSnackbarService();

    public PersonalDashboardWebContext()
    {
        Services.RemoveAll(typeof(HttpClient));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddJsonStream(CreateServiceDiscoveryConfigStream())
            .Build()
        );
        Services.AddPersonalDashboardWeb();
        Services.AddPersonalDashboardTestingServices();

        Services.RemoveAll(typeof(IHubConnectionFactory));
        Services.AddSingleton<IHubConnectionFactory>(HubFactory);
        Services.AddSingleton(HubFactory);

        Services.AddSingleton(Snackbar);
        Services.Replace(ServiceDescriptor.Singleton<ISnackbar>(Snackbar));

        var httpClient = new HttpClient(HttpHandler) { BaseAddress = new Uri("https://localhost:3000") };
        Services.RemoveAll(typeof(PersonalDashboardApiClient));
        Services.AddSingleton(sp =>
            new PersonalDashboardApiClient(httpClient, sp.GetRequiredService<IHubConnectionFactory>()));
    }

    private static Stream CreateServiceDiscoveryConfigStream()
    {
        var stream = new MemoryStream();
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Services = new
            {
                api = new
                {
                    https = new List<string> {
                        "https://localhost:3000"
                    },
                    http = new List<string> {
                        "https://localhost:3000"
                    }
                }
            }
        }));
        stream.Position = 0;
        return stream;
    }
}