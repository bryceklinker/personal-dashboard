using MediatR;

namespace Personal.Dashboard.Core.Common.Cqrs.Events;

public interface IEvent : INotification;

public interface IEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IEvent;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;
}

public class EventBus(IPublisher publisher) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        await publisher.Publish(@event, ct).ConfigureAwait(false);
    }
}
