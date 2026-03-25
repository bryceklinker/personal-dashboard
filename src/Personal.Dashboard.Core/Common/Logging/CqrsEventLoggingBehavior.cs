using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Cqrs.Events;

namespace Personal.Dashboard.Core.Common.Logging;

public class CqrsEventLoggingBehavior(IEventBus inner, ILoggerFactory loggerFactory) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        var logger = loggerFactory.CreateLogger<TEvent>();
        var stopwatch = new Stopwatch();
        Exception? exception = null;

        using (logger.BeginScope(new Dictionary<string, string>
               {
                   {"event", typeof(TEvent).Name},
               })
              )
        {
            try
            {
                logger.LogInformation("Publishing event...");
                stopwatch.Start();
                await inner.PublishAsync(@event, ct);
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (exception is not null)
                    logger.LogError(exception, "Failed event in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                else
                    logger.LogInformation("Published event in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
