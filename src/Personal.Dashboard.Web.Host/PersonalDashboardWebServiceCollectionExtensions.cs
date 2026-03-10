using MudBlazor.Services;
using Personal.Dashboard.Web.Host.Common.Apis;
using Personal.Dashboard.Web.Host.Common.SignalR;

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
            });
        services.AddHttpClient<PersonalDashboardApiClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https+http://api");
            });
        services.AddMudServices();
        services.AddSingleton<IHubConnectionFactory, HubConnectionFactory>();
        return services;
    }
}