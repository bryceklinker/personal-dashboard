using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Cqrs.Commands;

namespace Personal.Dashboard.Core.Common.Cqrs;

public class CqrsLoggingBehavior<TRequest, TResult>(ILoggerFactory loggerFactory) : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    public async Task<TResult> Handle(TRequest request, RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<TRequest>();
        var stopwatch = new Stopwatch();
        var requestType = request is ICommand || request is ICommand<TResult>
            ? "command"
            : "";
        Exception? exception = null;
        
        using (logger.BeginScope(new Dictionary<string, string>
               {
                   {requestType, typeof(TRequest).Name},
               })
              )
        {
            try
            {
                logger.LogInformation("Starting {RequestType}...", requestType);
                return await next(cancellationToken);
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
                    logger.LogError(exception, "Failed {RequestType} in {ElapsedMilliseconds} ms", requestType, stopwatch.ElapsedMilliseconds);
                else 
                    logger.LogInformation("Finished {RequestType} in {ElapsedMilliseconds} ms", requestType, stopwatch.ElapsedMilliseconds);
            }
            
        }
    }
}