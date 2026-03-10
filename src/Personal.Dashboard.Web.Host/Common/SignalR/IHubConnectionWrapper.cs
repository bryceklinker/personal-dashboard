using Microsoft.AspNetCore.SignalR.Client;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Web.Host.Common.SignalR;

public interface IHubConnectionWrapper
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    IDisposable On(string methodName, Func<DashboardEvent, Task> handler);
}

public interface IHubConnectionFactory
{
    IHubConnectionWrapper Create(Uri url);
}

public class HubConnectionWrapper(HubConnection connection) : IHubConnectionWrapper
{
    public Task StartAsync(CancellationToken ct = default) => connection.StartAsync(ct);
    public Task StopAsync(CancellationToken ct = default) => connection.StopAsync(ct);

    public IDisposable On(string methodName, Func<DashboardEvent, Task> handler)
        => connection.On<DashboardEvent>(methodName, async e => await handler(e));
}

public class HubConnectionFactory : IHubConnectionFactory
{
    public IHubConnectionWrapper Create(Uri url)
    {
        var connection = new HubConnectionBuilder().WithUrl(url).Build();
        return new HubConnectionWrapper(connection);
    }
}
