using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core;

namespace Personal.Dashboard.Api.Host;

public static class PersonalDashboardApiServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalDashboardApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddPersonalDashboardCore(opts =>
        {
            opts.AddAssembly(typeof(PersonalDashboardApiServiceCollectionExtensions).Assembly);
            opts.ConfigureDbContext = ctx =>
            {
                var connectionString = configuration.GetConnectionString("db");
                ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
                ctx.UseNpgsql(connectionString);
            };
        });
        return services;
    }
}