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
        services.AddHttpClient("api")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https+http://api");
            });
        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var hubFactory = sp.GetRequiredService<IHubConnectionFactory>();
            return new PersonalDashboardApiClient(factory.CreateClient("api"), hubFactory);
        });
        services.AddHttpClient("hub").AddServiceDiscovery();
        services.AddMudServices();
        services.AddSingleton<IHubConnectionFactory, HubConnectionFactory>();
        return services;
    }
}