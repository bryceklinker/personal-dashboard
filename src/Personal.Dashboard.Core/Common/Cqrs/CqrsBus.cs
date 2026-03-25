using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;

namespace Personal.Dashboard.Core.Common.Cqrs;

public interface ICqrsBus : ICommandBus, IQueryBus, IEventBus
{
}

public class CqrsBus(
    ICommandBus commandBus,
    IQueryBus queryBus,
    IEventBus eventBus
) : ICqrsBus
{
    public async Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await commandBus.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        return await commandBus.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
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