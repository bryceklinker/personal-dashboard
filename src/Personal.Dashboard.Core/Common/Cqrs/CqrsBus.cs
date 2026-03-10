using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;

namespace Personal.Dashboard.Core.Common.Cqrs;

public interface ICqrsBus : ICommandBus, IQueryBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;
}

public class CqrsBus(
    ICommandBus commandBus,
    IQueryBus queryBus,
    IEventBus eventBus
) : ICqrsBus
{
    public async Task ExecuteAsync(ICommand command)
    {
        await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        return await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        return await queryBus.QueryAsync(query).ConfigureAwait(false);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        await eventBus.PublishAsync(@event, ct).ConfigureAwait(false);
    }
}