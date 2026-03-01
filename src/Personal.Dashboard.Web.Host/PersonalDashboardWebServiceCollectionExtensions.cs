using MudBlazor.Services;

namespace Personal.Dashboard.Web.Host;

public static class PersonalDashboardWebServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalDashboardWeb(this IServiceCollection services)
    {
        services.AddServiceDiscovery();
        services.AddHttpClient()
            .ConfigureHttpClientDefaults(http =>
            {
                http.AddServiceDiscovery();
                http.ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https+http://api");
                });
            });
        services.AddMudServices();
        return services;
    }
}