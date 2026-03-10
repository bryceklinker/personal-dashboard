using System.Net.Http.Json;
using Personal.Dashboard.Models;
using Personal.Dashboard.Web.Host.Common.SignalR;

namespace Personal.Dashboard.Web.Host.Common.Apis;

public class PersonalDashboardApiClient(HttpClient client, IHubConnectionFactory hubConnectionFactory)
{
    private IHubConnectionWrapper? _hubConnection;
    private readonly Dictionary<string, List<Func<Task>>> _subscribers = new();

    public async Task StartListeningAsync(CancellationToken ct = default)
    {
        _hubConnection = hubConnectionFactory.Create(new Uri(client.BaseAddress!, "hubs/events"));
        _hubConnection.On("ReceiveEvent", (@event) => InvokeSubscribersAsync(@event.Type));
        await _hubConnection.StartAsync(ct);
    }

    public async Task StopListeningAsync(CancellationToken ct = default)
    {
        if (_hubConnection is not null)
            await _hubConnection.StopAsync(ct);
    }

    public IDisposable Subscribe<TEvent>(Action handler) where TEvent : DashboardEvent, new()
        => Subscribe<TEvent>(() => { handler(); return Task.CompletedTask; });

    public IDisposable Subscribe<TEvent>(Func<Task> handler) where TEvent : DashboardEvent, new()
    {
        var eventType = new TEvent().Type;
        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers = [];
            _subscribers[eventType] = handlers;
        }
        handlers.Add(handler);
        return new SubscriptionDisposable(() => handlers.Remove(handler));
    }

    public async Task RefreshLeaguesAsync(CancellationToken ct = default)
    {
        var response = await client.PostAsync("/leagues/refresh", null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedListResultModel<FootballLeagueModel>> GetLeaguesAsync(
        PagedListParameters? parameters = null)
    {
        var queryParameters = parameters ?? PagedListParameters.Default();
        return await GetJsonAsync<PagedListResultModel<FootballLeagueModel>>(
            $"/leagues?{queryParameters.ToQueryString()}"
        ).ConfigureAwait(false);
    }

    private Task InvokeSubscribersAsync(string eventType)
    {
        if (!_subscribers.TryGetValue(eventType, out var handlers))
            return Task.CompletedTask;
        return Task.WhenAll(handlers.ToList().Select(h => h()));
    }

    private async Task<TResult> GetJsonAsync<TResult>(string route)
    {
        var response = await client.GetAsync(route).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TResult>().ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("request returned null json result");
    }

    private sealed class SubscriptionDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
