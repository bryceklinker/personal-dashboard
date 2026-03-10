using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Common.SignalR;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public class PersonalDashboardWebContext : BunitContext
{
    public FakeHttpMessageHandler HttpHandler => Services.GetRequiredService<FakeHttpMessageHandler>();
    public FakeHubConnectionFactory HubFactory => Services.GetRequiredService<FakeHubConnectionFactory>();

    public PersonalDashboardWebContext()
    {
        Services.RemoveAll(typeof(HttpClient));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddJsonStream(CreateServiceDiscoveryConfigStream())
            .Build()
        );
        Services.AddPersonalDashboardWeb();
        Services.AddPersonalDashboardTestingServices();

        var fakeFactory = new FakeHubConnectionFactory();
        Services.RemoveAll(typeof(IHubConnectionFactory));
        Services.AddSingleton<IHubConnectionFactory>(fakeFactory);
        Services.AddSingleton(fakeFactory);
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