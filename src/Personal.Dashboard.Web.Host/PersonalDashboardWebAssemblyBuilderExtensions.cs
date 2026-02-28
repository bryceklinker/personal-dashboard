using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace Personal.Dashboard.Web.Host;

public static class PersonalDashboardWebAssemblyBuilderExtensions
{
    public static WebAssemblyHostBuilder AddPersonalDashboardWeb(
        this WebAssemblyHostBuilder builder
    )
    {
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpClient()
            .ConfigureHttpClientDefaults(http =>
            {
                http.AddServiceDiscovery();
                http.ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https+http://api");
                });
            });
        builder.Services.AddMudServices();
        return builder;
    }
}