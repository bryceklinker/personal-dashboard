using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Personal.Dashboard.Web.Host;

public static class PersonalDashboardWebAssemblyBuilderExtensions
{
    public static WebAssemblyHostBuilder AddPersonalDashboardWeb(
        this WebAssemblyHostBuilder builder
    )
    {
        builder.Services.AddPersonalDashboardWeb();
        return builder;
    }
}