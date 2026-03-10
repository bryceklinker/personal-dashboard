using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;

namespace Personal.Dashboard.Core.Tests.Support;

public class CapturingCqrsBus(ICqrsBus inner) : ICqrsBus
{
    private readonly ConcurrentBag<ICommand> _capturedCommands = [];
    private readonly ConcurrentBag<object> _capturedQueries = [];
    private readonly ConcurrentBag<IEvent> _capturedEvents = [];

    public IEnumerable<T> GetCapturedCommands<T>() where T : ICommand
        => _capturedCommands.OfType<T>();

    public IEnumerable<T> GetCapturedEvents<T>() where T : IEvent
        => _capturedEvents.OfType<T>();

    public IEnumerable<T> GetCapturedQueries<T>()
        => _capturedQueries.OfType<T>();

    public async Task ExecuteAsync(ICommand command)
    {
        _capturedCommands.Add(command);
        await inner.ExecuteAsync(command);
    }

    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        _capturedCommands.Add(command);
        return await inner.ExecuteAsync(command);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        _capturedQueries.Add(query);
        return await inner.QueryAsync(query);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        _capturedEvents.Add(@event);
        await inner.PublishAsync(@event, ct);
    }
}

public static class CapturingCqrsBusServiceCollectionExtensions
{
    public static IServiceCollection AddCapturingCqrsBus(this IServiceCollection services)
    {
        services.AddSingleton<CapturingCqrsBus>(sp =>
        {
            var commandBus = sp.GetRequiredService<ICommandBus>();
            var queryBus = sp.GetRequiredService<IQueryBus>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            var realBus = new CqrsBus(commandBus, queryBus, eventBus);
            return new CapturingCqrsBus(realBus);
        });
        services.AddSingleton<ICqrsBus>(sp => sp.GetRequiredService<CapturingCqrsBus>());
        return services;
    }
}
