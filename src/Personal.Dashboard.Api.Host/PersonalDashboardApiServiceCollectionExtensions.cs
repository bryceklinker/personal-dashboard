using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Api.Host.Common.SignalR;
using Personal.Dashboard.Api.Host.Leagues;
using Personal.Dashboard.Core;

namespace Personal.Dashboard.Api.Host;

public static class PersonalDashboardApiServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalDashboardApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(cors =>
        {
            cors.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
            });
        });
        services.AddControllers();
        services.AddSignalR();
        services.AddTransient<ISignalRPublisher, SignalRPublisher>();
        services.AddOptions<RefreshLeaguesSettings>()
            .BindConfiguration(RefreshLeaguesSettings.SectionName);
        services.AddHostedService<RefreshLeaguesBackgroundService>();
        services.AddPersonalDashboardCore(opts =>
        {
            opts.AddAssembly(typeof(PersonalDashboardApiServiceCollectionExtensions).Assembly);
            var connectionString = configuration.GetConnectionString("db");
            if (string.IsNullOrEmpty(connectionString))
                return;
            opts.ConfigureDbContext = ctx =>
            {
                if (configuration.HasDbConnectionString()) ctx.UseNpgsql(connectionString);
            };
        });
        return services;
    }
}