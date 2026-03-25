using System.Reflection;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Logging;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Common.Validation;

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

        services.AddPersonalDashboardDbContext(options.ConfigureDbContext);
        services.AddFootballApiClient(options.ConfigureFootballApi);
        services.AddPersonalDashboardCqrs(options.Assemblies.ToArray());
        
        services.AddValidatorsFromAssemblies(options.Assemblies);
        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(options.Assemblies);
        });
        return services;
    }

    public static IServiceCollection AddPersonalDashboardCqrs(this IServiceCollection services, Assembly[] assemblies)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
            cfg.AddOpenBehavior(typeof(CqrsLoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(CqrsValidationPipelineBehavior<,>));
        });
        services.AddTransient<IQueryBus, QueryBus>();
        services.AddTransient<ICommandBus, CommandBus>();
        services.AddTransient<EventBus>();
        services.AddTransient<IEventBus>(sp =>
            new CqrsEventLoggingBehavior(sp.GetRequiredService<EventBus>(), sp.GetRequiredService<ILoggerFactory>()));
        services.AddTransient<CqrsBus>();
        services.AddTransient<ICqrsBus>(sp => sp.GetRequiredService<CqrsBus>());
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

    public static IServiceCollection AddPersonalDashboardDbContext(this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configure)
    {
        services.AddDbContext<PersonalDashboardContext>(db =>
        {
            if (configure is not null)
            {
                configure(db);
            }
        });
        return services;
    }
}