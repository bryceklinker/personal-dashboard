using System.Collections.Concurrent;
using Personal.Dashboard.Models;
using Personal.Dashboard.Web.Host.Common.SignalR;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public class FakeHubConnectionWrapper : IHubConnectionWrapper
{
    private readonly ConcurrentBag<Func<DashboardEvent, Task>> _handlers = [];

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

    public IDisposable On(string methodName, Func<DashboardEvent, Task> handler)
    {
        _handlers.Add(handler);
        return new FakeDisposable(() => _handlers.TryTake(out _));
    }

    public Task SimulateEventAsync(DashboardEvent @event)
        => Task.WhenAll(_handlers.Select(h => h(@event)));

    public void SimulateEvent(DashboardEvent @event)
        => _ = SimulateEventAsync(@event);

    private sealed class FakeDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

public class FakeHubConnectionFactory : IHubConnectionFactory
{
    public FakeHubConnectionWrapper Connection { get; } = new();
    public IHubConnectionWrapper Create(Uri url) => Connection;
}
