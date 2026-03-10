# Leagues Refresh Feature Design

## Overview

Add the ability to refresh the list of football leagues from the external API (api-football.com), store them in the database via upsert, and automatically notify the Blazor UI via SignalR when the data changes.

---

## Section 1 — CQRS Infrastructure

Three new abstractions added to `Personal.Dashboard.Core/Common/Cqrs`, following the existing `ICommand`/`IQuery` pattern:

- **`IEvent`** — marker interface inheriting `INotification` (MediatR)
- **`IEventHandler<TEvent>`** — inherits `INotificationHandler<TEvent>` where `TEvent : IEvent`
- **`IEventBus`** — single method: `PublishAsync<TEvent>(TEvent @event, CancellationToken ct)`
- **`EventBus`** — implements `IEventBus`, wraps MediatR's `IPublisher`

`ICqrsBus` gains a `PublishAsync` method. `CqrsBus` accepts `IEventBus` via constructor injection and delegates to it. Command/query handlers never touch MediatR directly — `ICqrsBus.PublishAsync` is the only publish surface.

---

## Section 2 — Refresh Command, Upsert Logic & Data Model Changes

### Data Model Changes

**`FootballLeagueEntity`** gains:
```csharp
public DateTimeOffset? LastRefreshed { get; set; }
```
Nullable so existing leagues with no refresh history have a clear "never refreshed" state.

**`FootballLeagueModel`** gains:
```csharp
DateTimeOffset? LastRefreshed
```
AutoMapper profile updated to include it. A new EF Core migration adds the nullable `timestamptz` column.

### RefreshLeaguesCommand

New `RefreshLeaguesCommand` (no parameters) added to `Personal.Dashboard.Core/Leagues/Commands/`. The existing `DownloadLeagueCommand` (download by name) is left unchanged.

**Upsert logic:**
1. Fetch all leagues from `IFootballApiClient.GetLeaguesAsync()` with no filters
2. Load existing `FootballLeagueAlias` records where `AliasSource == DataSource.FootballApi`
3. For each API league: if alias exists → update `Name` and `LastRefreshed = DateTimeOffset.UtcNow`; if not → create new entity with alias and `LastRefreshed`
4. Save changes
5. Call `bus.PublishAsync(new LeaguesRefreshedEvent())`

**`LeaguesRefreshedEvent`** — carries no payload. It is a pure notification that the league dataset changed.

---

## Section 3 — SignalR Hub & Event Handler

### DashboardEvent Hierarchy

Lives in `Personal.Dashboard.Models` (shared by API and Blazor client):

```csharp
public record DashboardEvent(string Type, object? Payload = null);
public record LeaguesRefreshedDashboardEvent() : DashboardEvent("LeaguesRefreshed");
```

No magic strings at call sites. Future events add a new record inheriting `DashboardEvent`.

### EventsHub

`EventsHub` (inherits `Hub`) is added to `Api.Host`, mapped at `/hubs/events`. It is a thin server-push-only hub — no client-callable methods needed.

### ISignalRPublisher

Lives in `Api.Host`. Owns all SignalR transport concerns:

```csharp
public interface ISignalRPublisher
{
    Task PublishAsync(DashboardEvent @event, CancellationToken ct = default);
}
```

`SignalRPublisher` implements it by injecting `IHubContext<EventsHub>` and calling:
```csharp
Clients.All.SendAsync("ReceiveEvent", @event)
```
The method name `"ReceiveEvent"` is encapsulated here — no other class references it.

### LeaguesRefreshedEventHandler

Lives in `Api.Host`. Implements `IEventHandler<LeaguesRefreshedEvent>`, injects only `ISignalRPublisher`:

```csharp
await publisher.PublishAsync(new LeaguesRefreshedDashboardEvent(), ct);
```

SignalR is wired via `AddSignalR()` and `MapHub<EventsHub>("/hubs/events")` in the existing `AddPersonalDashboardApi` / `UsePersonalDashboardApi` extension methods.

---

## Section 4 — Scheduled Job & On-Demand Endpoint

### RefreshLeaguesBackgroundService

Added to `Api.Host`. Uses `PeriodicTimer`, dispatches `RefreshLeaguesCommand` via `ICqrsBus` on each tick. Interval is configurable:

```json
"RefreshLeagues": {
  "IntervalHours": 24
}
```

Default is **24 hours** if configuration is absent — protects against accidental API rate limit hits.

Because `BackgroundService` is singleton and `ICqrsBus`/`PersonalDashboardContext` are scoped, the service injects `IServiceScopeFactory` and creates a scope per tick.

### On-Demand Endpoint

New action on `LeaguesController`:

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshLeagues()
{
    return await CommandAsync(new RefreshLeaguesCommand());
}
```

Triggers the same command as the scheduled job. No logic duplication.

---

## Section 5 — Blazor UI Changes

### PersonalDashboardApiClient as Unified Facade

The client hides all HTTP and SignalR details. New members:

```csharp
Task StartListeningAsync(CancellationToken ct = default);
Task StopListeningAsync(CancellationToken ct = default);
IDisposable Subscribe<TEvent>(Action handler) where TEvent : DashboardEvent;
Task RefreshLeaguesAsync(); // POST /leagues/refresh
```

Internally holds a `HubConnection` to `/hubs/events`. Registers a single `"ReceiveEvent"` handler that deserializes the incoming `DashboardEvent`, matches `Type`, and fans out to registered subscribers.

### App Startup

`StartListeningAsync` is called once in `App.razor` on initialization. `StopListeningAsync` is called on teardown. No page or component manages a connection directly.

### LeaguesList.razor Changes

- Calls `Client.Subscribe<LeaguesRefreshedDashboardEvent>(() => RefetchLeagues())` in `OnInitializedAsync`
- Disposes the subscription in `IAsyncDisposable.DisposeAsync`
- On event receipt, re-fetches the current page using existing pagination state and calls `StateHasChanged()`
- Adds a `MudIconButton` (refresh icon) that calls `Client.RefreshLeaguesAsync()` — the UI update arrives via SignalR, keeping manual and scheduled flows consistent
- Displays `LastRefreshed` per league item (e.g., formatted timestamp)

---

## Section 6 — Testing Strategy

### CapturingCqrsBus (Test.Support)

A decorator wrapping the real `ICqrsBus`. Executes the full normal flow of all commands, queries, and events — no mocking — but records everything that passes through it. Tests assert on captured items:

```csharp
capturingBus.CapturedEvents.OfType<LeaguesRefreshedEvent>()
```

### Personal.Dashboard.Core.Tests

**`RefreshLeaguesCommandTests`**
- Uses `CapturingCqrsBus` to verify `LeaguesRefreshedEvent` was published
- Verifies upsert: new leagues are created with aliases; existing leagues (matched by Football API alias) have `Name` and `LastRefreshed` updated

### Personal.Dashboard.Api.Host.Tests

**`LeaguesApiTests`**
- Uses `WebApplicationFactory` to spin up the full app
- Connects a real SignalR client to `/hubs/events`
- Calls `POST /leagues/refresh`
- Asserts a `LeaguesRefreshedDashboardEvent` arrives on the SignalR connection
- Covers the full chain: command → event → handler → SignalR publisher → hub → client. No separate handler or publisher tests needed.

### Personal.Dashboard.Web.Host.Tests

**`LeaguesListTests`**
- Uses existing `FakeHttpMessageHandler` for HTTP responses
- A new `FakeSignalRConnection` (in `Test.Support`) mirrors `FakeHttpMessageHandler` — can be configured to emit a `DashboardEvent` and records outbound calls. Injected in place of the real SignalR infrastructure inside `PersonalDashboardApiClient`
- Tests simulate a `LeaguesRefreshedDashboardEvent`, assert the component re-fetches and re-renders with updated data including `LastRefreshed`
- Tests verify the refresh button triggers `POST /leagues/refresh` via `FakeHttpMessageHandler`
