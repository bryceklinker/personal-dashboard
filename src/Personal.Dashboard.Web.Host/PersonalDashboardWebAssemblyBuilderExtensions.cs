using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace Personal.Dashboard.Web.Host;

public static class PersonalDashboardWebAssemblyBuilderExtensions
{
    public static WebAssemblyHostBuilder AddPersonalDashboardWeb(
        this WebAssemblyHostBuilder builder
    )
    {
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        builder.Services.AddMudServices();
        return builder;
    }
}