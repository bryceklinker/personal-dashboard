using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core;

public static class PersonalDashboardCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalDashboardCore(
        this IServiceCollection services
        )
    {
        return services.AddPersonalDashboardCore(_ => { });
    }

    public static IServiceCollection AddPersonalDashboardCore(this IServiceCollection services,
        Action<PersonalDashboardCoreOptions> configure)
    {
        var options = new PersonalDashboardCoreOptions(
            typeof(PersonalDashboardCoreServiceCollectionExtensions).Assembly
        );
        configure(options);

        services.AddFootballApiClient(options.ConfigureFootballApi);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(options.Assemblies.ToArray());
        });
        services.AddValidatorsFromAssemblies(options.Assemblies);
        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(options.Assemblies);
        });
        return services;
    }

    public static IServiceCollection AddFootballApiClient(this IServiceCollection services,
        Action<FootballApiClientSettings>? configure)
    {
        if (configure is not null)
        {
            services.AddOptions<FootballApiClientSettings>()
                .Configure(configure);    
        }

        services.AddHttpClient(FootballApiClientSettings.ClientName);
        services.AddTransient<IFootballApiClient, FootballApiClient>();
        return services;
    }
}