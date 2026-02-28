using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Migrations.Host;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddPersonalDashboardMigrations(this HostApplicationBuilder builder, IConfiguration configuration)
    {
        builder.AddServiceDefaults();
        builder.Services.AddPersonalDashboardCore(opts =>
        {
            opts.AddAssembly(typeof(HostApplicationBuilderExtensions).Assembly);
            opts.ConfigureDbContext = ctx =>
            {
                var connectionString = configuration.GetConnectionString("db");
                ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
                ctx.UseNpgsql(connectionString, dbOpts =>
                {
                    dbOpts.MigrationsAssembly(typeof(HostApplicationBuilderExtensions).Assembly);
                });
            };
        });
        return builder;
    }
}