using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Migrations.Host;

public class MigrationsWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostLifetime,
    ILogger<MigrationsWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PersonalDashboardContext>();
            await RunMigrationsAsync(context, stoppingToken);
            logger.LogInformation("finished migrating database");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to migrate database");
        }
        finally
        {
            hostLifetime.StopApplication();    
        }
    }

    private async Task RunMigrationsAsync(PersonalDashboardContext context, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await context.Database.MigrateAsync(cancellationToken: cancellationToken);
        });
    }
}