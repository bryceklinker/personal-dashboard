using Microsoft.Extensions.Options;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Leagues.Commands;

namespace Personal.Dashboard.Api.Host.Leagues;

public class RefreshLeaguesBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshLeaguesSettings> settings,
    ILogger<RefreshLeaguesBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(settings.Value.IntervalHours);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<ICqrsBus>();
            try
            {
                await bus.ExecuteAsync(new RefreshLeaguesCommand());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled league refresh failed");
            }
        }
    }
}
