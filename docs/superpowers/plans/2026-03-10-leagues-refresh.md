# Leagues Refresh Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automatic and on-demand refresh of football leagues from the external API, store them via upsert, and notify the Blazor UI in real-time via SignalR when data changes.

**Architecture:** `RefreshLeaguesCommand` fetches all leagues from the football API, upserts them in the database, and publishes a `LeaguesRefreshedEvent` via `ICqrsBus.PublishAsync`. An event handler in `Api.Host` sends a `LeaguesRefreshedDashboardEvent` to all connected SignalR clients via `ISignalRPublisher`. The Blazor client subscribes through `PersonalDashboardApiClient`, which is the single facade for both HTTP and SignalR communication.

**Tech Stack:** .NET 10, MediatR 14, EF Core 10, SignalR, Blazor WebAssembly, MudBlazor 9, xUnit, bUnit

---

## File Map

**New files:**
- `src/Personal.Dashboard.Core/Common/Cqrs/Events/EventBus.cs` — `IEvent`, `IEventHandler<T>`, `IEventBus`, `EventBus`
- `src/Personal.Dashboard.Core/Leagues/Events/LeaguesRefreshedEvent.cs` — `LeaguesRefreshedEvent : IEvent`
- `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs` — command + handler
- `src/Personal.Dashboard.Models/DashboardEvents.cs` — `DashboardEvent`, `LeaguesRefreshedDashboardEvent`
- `src/Personal.Dashboard.Api.Host/Common/SignalR/EventsHub.cs` — SignalR hub at `/hubs/events`
- `src/Personal.Dashboard.Api.Host/Common/SignalR/ISignalRPublisher.cs` — interface + `SignalRPublisher`
- `src/Personal.Dashboard.Api.Host/Leagues/LeaguesRefreshedEventHandler.cs` — `IEventHandler<LeaguesRefreshedEvent>`
- `src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesSettings.cs` — settings with 24h default
- `src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesBackgroundService.cs` — `PeriodicTimer` background service
- `src/Personal.Dashboard.Web.Host/Common/SignalR/IHubConnectionWrapper.cs` — `IHubConnectionWrapper`, `HubConnectionWrapper`, `IHubConnectionFactory`, `HubConnectionFactory`
- `tests/Personal.Dashboard.Test.Support/Common/Cqrs/CapturingCqrsBus.cs` — decorator that records + forwards
- `tests/Personal.Dashboard.Test.Support/Common/SignalR/FakeHubConnectionFactory.cs` — `FakeHubConnectionFactory`, `FakeHubConnectionWrapper`
- `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs`
- `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesRefreshApiTests.cs`

**Modified files:**
- `src/Personal.Dashboard.Core/Common/Cqrs/CqrsBus.cs` — add `IEventBus` to `ICqrsBus`/`CqrsBus`
- `src/Personal.Dashboard.Core/PersonalDashboardCoreServiceCollectionExtensions.cs` — register `EventBus`; register `CqrsBus` as concrete type
- `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs` — add `LastRefreshed`
- `src/Personal.Dashboard.Models/FootballModels.cs` — add `LastRefreshed` to `FootballLeagueModel`
- `src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs` — add `POST /leagues/refresh`
- `src/Personal.Dashboard.Api.Host/PersonalDashboardApiServiceCollectionExtensions.cs` — register SignalR, `ISignalRPublisher`, settings, background service
- `src/Personal.Dashboard.Api.Host/Program.cs` — `MapHub<EventsHub>("/hubs/events")`
- `src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs` — add `StartListeningAsync`, `StopListeningAsync`, `Subscribe<TEvent>` (Action + Func<Task> overloads), `RefreshLeaguesAsync`
- `src/Personal.Dashboard.Web.Host/PersonalDashboardWebServiceCollectionExtensions.cs` — register `IHubConnectionFactory`
- `src/Personal.Dashboard.Web.Host/App.razor` — call `StartListeningAsync`/`StopListeningAsync`
- `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor` — subscribe to event, refresh button, show `LastRefreshed`
- `tests/Personal.Dashboard.Api.Host.Tests/Support/PersonalDashboardApiApplication.cs` — expose `HttpHandler`, configure Football API base URL
- `tests/Personal.Dashboard.Test.Support/PersonalDashboardTestingServicesCollectionExtensions.cs` — add `AddCapturingCqrsBus`
- `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs` — add `SetupRefreshLeagues`
- `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardWebContext.cs` — register `FakeHubConnectionFactory`
- `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs` — add refresh button + SignalR event tests
- `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardCoreTestingProviderFactory.cs` — add `configureServices` parameter

---

## Chunk 1: CQRS Event Infrastructure

### Task 1: Add IEvent, IEventHandler, IEventBus, EventBus

**Files:**
- Create: `src/Personal.Dashboard.Core/Common/Cqrs/Events/EventBus.cs`
- Modify: `src/Personal.Dashboard.Core/Common/Cqrs/CqrsBus.cs`
- Modify: `src/Personal.Dashboard.Core/PersonalDashboardCoreServiceCollectionExtensions.cs`

- [ ] **Step 1: Create `EventBus.cs`**

```csharp
// src/Personal.Dashboard.Core/Common/Cqrs/Events/EventBus.cs
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
```

- [ ] **Step 2: Update `CqrsBus.cs` — add `PublishAsync` to `ICqrsBus`, inject `IEventBus` into `CqrsBus`**

```csharp
// src/Personal.Dashboard.Core/Common/Cqrs/CqrsBus.cs
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
```

- [ ] **Step 3: Update `AddPersonalDashboardCqrs` to register `EventBus` and expose `CqrsBus` as its concrete type**

In `src/Personal.Dashboard.Core/PersonalDashboardCoreServiceCollectionExtensions.cs`:

```csharp
// Add using:
using Personal.Dashboard.Core.Common.Cqrs.Events;

// Replace AddPersonalDashboardCqrs method:
public static IServiceCollection AddPersonalDashboardCqrs(this IServiceCollection services, Assembly[] assemblies)
{
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblies(assemblies);
        cfg.AddOpenBehavior(typeof(CqrsLoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(CqrsValidationPipelineBehavior<,>));
    });
    services.AddTransient<IQueryBus, QueryBus>();
    services.AddTransient<ICommandBus, CommandBus>();
    services.AddTransient<IEventBus, EventBus>();
    services.AddTransient<CqrsBus>();
    services.AddTransient<ICqrsBus>(sp => sp.GetRequiredService<CqrsBus>());
    return services;
}
```

- [ ] **Step 4: Build and run all tests**

```bash
dotnet build --no-restore
dotnet test --no-build --no-restore
```
Expected: Build succeeded, all existing tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Common/Cqrs/Events/EventBus.cs \
        src/Personal.Dashboard.Core/Common/Cqrs/CqrsBus.cs \
        src/Personal.Dashboard.Core/PersonalDashboardCoreServiceCollectionExtensions.cs
git commit -m "feat: add IEvent/IEventBus CQRS event infrastructure"
```

---

### Task 2: Add CapturingCqrsBus to Test.Support

**Files:**
- Create: `tests/Personal.Dashboard.Test.Support/Common/Cqrs/CapturingCqrsBus.cs`
- Modify: `tests/Personal.Dashboard.Test.Support/PersonalDashboardTestingServicesCollectionExtensions.cs`

- [ ] **Step 1: Create `CapturingCqrsBus`**

Use `ConcurrentBag` for thread safety. Expose captured items via typed methods rather than public collections so tests cannot mutate recorded state directly.

```csharp
// tests/Personal.Dashboard.Test.Support/Common/Cqrs/CapturingCqrsBus.cs
using System.Collections.Concurrent;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;

namespace Personal.Dashboard.Test.Support.Common.Cqrs;

public class CapturingCqrsBus(ICqrsBus inner) : ICqrsBus
{
    private readonly ConcurrentBag<ICommand> _capturedCommands = [];
    private readonly ConcurrentBag<object> _capturedQueries = [];
    private readonly ConcurrentBag<object> _capturedEvents = [];

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
```

- [ ] **Step 2: Add `AddCapturingCqrsBus` to `PersonalDashboardTestingServicesCollectionExtensions`**

`CqrsBus` is constructed directly from `ICommandBus`, `IQueryBus`, and `IEventBus` — never via `sp.GetRequiredService<ICqrsBus>()` — to avoid circular DI resolution.

```csharp
// Add usings:
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Test.Support.Common.Cqrs;

// Add method to the class:
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
    services.ReplaceService<ICqrsBus, CapturingCqrsBus>(sp =>
        sp.GetRequiredService<CapturingCqrsBus>());
    return services;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build tests/Personal.Dashboard.Test.Support --no-restore
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add tests/Personal.Dashboard.Test.Support/Common/Cqrs/CapturingCqrsBus.cs \
        tests/Personal.Dashboard.Test.Support/PersonalDashboardTestingServicesCollectionExtensions.cs
git commit -m "feat: add CapturingCqrsBus test helper"
```

---

## Chunk 2: Data Model + RefreshLeaguesCommand

### Task 3: Add LastRefreshed to entity, model, and migration

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`
- Modify: `src/Personal.Dashboard.Models/FootballModels.cs`

- [ ] **Step 1: Add `LastRefreshed` to `FootballLeagueEntity`**

```csharp
public DateTimeOffset? LastRefreshed { get; set; }
```

No EF configuration change needed — EF Core maps `DateTimeOffset?` to a nullable `timestamptz` automatically.

- [ ] **Step 2: Update `FootballLeagueModel` record**

```csharp
public record FootballLeagueModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed
);
```

- [ ] **Step 3: Build — fix any compile errors**

```bash
dotnet build --no-restore
```

Update `DataFactory.FootballLeagueModel()` to include `LastRefreshed: null`. Fix any tests constructing `FootballLeagueModel` directly by adding `null` as the third argument.

- [ ] **Step 4: Run all tests**

```bash
dotnet test --no-build --no-restore
```
Expected: All pass.

- [ ] **Step 5: Add EF Core migration**

```bash
dotnet ef migrations add AddLeagueLastRefreshed \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```
Expected: New migration file created in `src/Personal.Dashboard.Migrations.Host/Migrations/`.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs \
        src/Personal.Dashboard.Models/FootballModels.cs \
        src/Personal.Dashboard.Migrations.Host/Migrations/ \
        tests/
git commit -m "feat: add LastRefreshed to league entity, model, and migration"
```

---

### Task 4: Add LeaguesRefreshedEvent and RefreshLeaguesCommand (TDD)

**Files:**
- Create: `src/Personal.Dashboard.Core/Leagues/Events/LeaguesRefreshedEvent.cs`
- Create: `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardCoreTestingProviderFactory.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs`

- [ ] **Step 1: Create `LeaguesRefreshedEvent`**

```csharp
// src/Personal.Dashboard.Core/Leagues/Events/LeaguesRefreshedEvent.cs
using Personal.Dashboard.Core.Common.Cqrs.Events;

namespace Personal.Dashboard.Core.Leagues.Events;

public record LeaguesRefreshedEvent : IEvent;
```

- [ ] **Step 2: Update `PersonalDashboardCoreTestingProviderFactory` to accept a `configureServices` parameter**

```csharp
public static IServiceProvider Create(
    Action<PersonalDashboardCoreOptions>? configure = null,
    Action<IServiceCollection>? configureServices = null)
{
    return PersonalDashboardTestingProviderFactory.CreateProvider(services =>
    {
        services.AddPersonalDashboardCore(opts =>
        {
            opts.ConfigureDbContext = db =>
            {
                db.UseInMemoryDatabase($"{Guid.NewGuid():N}");
            };
            configure?.Invoke(opts);
        });
        configureServices?.Invoke(services);
    });
}
```

- [ ] **Step 3: Write the failing tests**

Use `with` syntax to set the Football API league ID directly in the test — no factory helper needed.

```csharp
// tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Cqrs;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class RefreshLeaguesCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshLeaguesCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(
            configure: opts => opts.ConfigureFootballApi = api => api.BaseUrl = BaseUrl,
            configureServices: services => services.AddCapturingCqrsBus()
        );
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
        _cqrsBus = provider.GetRequiredService<CapturingCqrsBus>();
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenCreatesNewLeaguesInDatabase()
    {
        var league = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [league]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dbLeagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Single(dbLeagues);
        Assert.Equal(league.League.Name, dbLeagues[0].Name);
        Assert.NotNull(dbLeagues[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenUpdatesExistingLeagueInDatabase()
    {
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague();
        existingLeague.AddAlias(DataSource.FootballApi, "42");
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        var apiLeague = FootballApiDataFactory.League();
        var apiLeagueWithKnownId = apiLeague with { League = apiLeague.League with { Id = 42 } };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeagueWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dbLeagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Single(dbLeagues);
        Assert.Equal(apiLeagueWithKnownId.League.Name, dbLeagues[0].Name);
        Assert.NotNull(dbLeagues[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenPublishesLeaguesRefreshedEvent()
    {
        await _handler.SetupGetLeagues(BaseUrl, [FootballApiDataFactory.League()]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        Assert.Single(_cqrsBus.GetCapturedEvents<LeaguesRefreshedEvent>());
    }
}
```

- [ ] **Step 4: Run to confirm tests fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --no-restore \
  --filter "FullyQualifiedName~RefreshLeaguesCommandTests"
```
Expected: FAIL — `RefreshLeaguesCommand` does not exist yet.

- [ ] **Step 5: Create `RefreshLeaguesCommand`**

```csharp
// src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs
using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record RefreshLeaguesCommand : ICommand;

public class RefreshLeaguesCommandHandler(
    IFootballApiClient client,
    PersonalDashboardContext context,
    ICqrsBus bus
) : ICommandHandler<RefreshLeaguesCommand>
{
    public async Task Handle(RefreshLeaguesCommand request, CancellationToken cancellationToken)
    {
        var response = await client.GetLeaguesAsync();

        var existingAliases = await context.Set<FootballLeagueAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.League)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var apiLeague in response.Response)
        {
            var aliasKey = $"{apiLeague.League.Id}";
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.League.Name = apiLeague.League.Name;
                alias.League.LastRefreshed = now;
            }
            else
            {
                var entity = new FootballLeagueEntity
                {
                    Name = apiLeague.League.Name,
                    LastRefreshed = now
                };
                entity.AddAlias(DataSource.FootballApi, aliasKey);
                context.Add(entity);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await bus.PublishAsync(new LeaguesRefreshedEvent(), cancellationToken);
    }
}
```

- [ ] **Step 6: Run tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --no-restore \
  --filter "FullyQualifiedName~RefreshLeaguesCommandTests"
```
Expected: All 3 tests PASS.

- [ ] **Step 7: Run all tests**

```bash
dotnet test --no-restore
```
Expected: All pass.

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Events/LeaguesRefreshedEvent.cs \
        src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs \
        tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardCoreTestingProviderFactory.cs
git commit -m "feat: add RefreshLeaguesCommand with upsert and event publishing"
```

---

## Chunk 3: SignalR Infrastructure + API Endpoint

### Task 5: Add DashboardEvent hierarchy to Models

**Files:**
- Create: `src/Personal.Dashboard.Models/DashboardEvents.cs`

- [ ] **Step 1: Create the event models**

```csharp
// src/Personal.Dashboard.Models/DashboardEvents.cs
namespace Personal.Dashboard.Models;

public record DashboardEvent(string Type, object? Payload = null);
public record LeaguesRefreshedDashboardEvent() : DashboardEvent("LeaguesRefreshed");
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/Personal.Dashboard.Models --no-restore
git add src/Personal.Dashboard.Models/DashboardEvents.cs
git commit -m "feat: add DashboardEvent hierarchy to shared models"
```

---

### Task 6: Add EventsHub and SignalR infrastructure to Api.Host

**Files:**
- Create: `src/Personal.Dashboard.Api.Host/Common/SignalR/EventsHub.cs`
- Create: `src/Personal.Dashboard.Api.Host/Common/SignalR/ISignalRPublisher.cs`
- Modify: `src/Personal.Dashboard.Api.Host/PersonalDashboardApiServiceCollectionExtensions.cs`
- Modify: `src/Personal.Dashboard.Api.Host/Program.cs`

- [ ] **Step 1: Create `EventsHub`**

```csharp
// src/Personal.Dashboard.Api.Host/Common/SignalR/EventsHub.cs
using Microsoft.AspNetCore.SignalR;

namespace Personal.Dashboard.Api.Host.Common.SignalR;

public class EventsHub : Hub;
```

- [ ] **Step 2: Create `ISignalRPublisher` and `SignalRPublisher`**

```csharp
// src/Personal.Dashboard.Api.Host/Common/SignalR/ISignalRPublisher.cs
using Microsoft.AspNetCore.SignalR;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Common.SignalR;

public interface ISignalRPublisher
{
    Task PublishAsync(DashboardEvent @event, CancellationToken ct = default);
}

public class SignalRPublisher(IHubContext<EventsHub> hubContext) : ISignalRPublisher
{
    public async Task PublishAsync(DashboardEvent @event, CancellationToken ct = default)
    {
        await hubContext.Clients.All.SendAsync("ReceiveEvent", @event, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Register SignalR and `ISignalRPublisher` in `AddPersonalDashboardApi`**

```csharp
// Add using:
using Personal.Dashboard.Api.Host.Common.SignalR;

// Add to AddPersonalDashboardApi method body:
services.AddSignalR();
services.AddTransient<ISignalRPublisher, SignalRPublisher>();
```

- [ ] **Step 4: Map the hub in `Program.cs`**

```csharp
// Add after app.MapControllers():
using Personal.Dashboard.Api.Host.Common.SignalR;

app.MapHub<EventsHub>("/hubs/events");
```

- [ ] **Step 5: Build and commit**

```bash
dotnet build src/Personal.Dashboard.Api.Host --no-restore
git add src/Personal.Dashboard.Api.Host/Common/SignalR/ \
        src/Personal.Dashboard.Api.Host/PersonalDashboardApiServiceCollectionExtensions.cs \
        src/Personal.Dashboard.Api.Host/Program.cs
git commit -m "feat: add EventsHub and SignalR publisher to API"
```

---

### Task 7: Add LeaguesRefreshedEventHandler and POST /leagues/refresh (TDD)

**Files:**
- Create: `src/Personal.Dashboard.Api.Host/Leagues/LeaguesRefreshedEventHandler.cs`
- Modify: `src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs`
- Modify: `tests/Personal.Dashboard.Api.Host.Tests/Support/PersonalDashboardApiApplication.cs`
- Create: `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesRefreshApiTests.cs`

- [ ] **Step 1: Update `PersonalDashboardApiApplication`**

```csharp
// tests/Personal.Dashboard.Api.Host.Tests/Support/PersonalDashboardApiApplication.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Api.Host.Tests.Support;

public class PersonalDashboardApiApplication : WebApplicationFactory<Program>
{
    public const string FootballApiBaseUrl = "https://football.api.test";

    public FakeHttpMessageHandler HttpHandler =>
        Services.GetRequiredService<FakeHttpMessageHandler>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddPersonalDashboardCore(opts =>
            {
                opts.ConfigureDbContext = ctx => ctx.UseInMemoryDatabase("in-memory-db");
                opts.ConfigureFootballApi = api => api.BaseUrl = FootballApiBaseUrl;
            });
            services.AddPersonalDashboardTestingServices();
        });
    }

    public async Task AddToDbAsync<T>(T entity) where T : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PersonalDashboardContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Write the failing integration tests**

```csharp
// tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesRefreshApiTests.cs
using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesRefreshApiTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenRefreshingLeaguesThenReturnsSuccess()
    {
        await app.HttpHandler.SetupGetLeagues(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            [FootballApiDataFactory.League()]
        );

        var response = await _client.PostAsync("/leagues/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenSendsLeaguesRefreshedEventViaSignalR()
    {
        await app.HttpHandler.SetupGetLeagues(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            [FootballApiDataFactory.League()]
        );

        DashboardEvent? receivedEvent = null;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(app.Server.BaseAddress, "hubs/events"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();

        connection.On<DashboardEvent>("ReceiveEvent", e => receivedEvent = e);
        await connection.StartAsync();

        await _client.PostAsync("/leagues/refresh", null);

        await Eventually.Assert(() =>
        {
            Assert.NotNull(receivedEvent);
            Assert.Equal("LeaguesRefreshed", receivedEvent.Type);
        });

        await connection.StopAsync();
    }
}
```

- [ ] **Step 3: Add `Microsoft.AspNetCore.SignalR.Client` to the API test project**

```bash
dotnet add tests/Personal.Dashboard.Api.Host.Tests \
  package Microsoft.AspNetCore.SignalR.Client
```

- [ ] **Step 4: Run to confirm tests fail**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --no-restore \
  --filter "FullyQualifiedName~LeaguesRefreshApiTests"
```
Expected: FAIL.

- [ ] **Step 5: Add `POST /leagues/refresh` to `LeaguesController`**

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshLeagues()
{
    return await ExecuteAsync(new RefreshLeaguesCommand());
}
```

- [ ] **Step 6: Create `LeaguesRefreshedEventHandler`**

```csharp
// src/Personal.Dashboard.Api.Host/Leagues/LeaguesRefreshedEventHandler.cs
using Personal.Dashboard.Api.Host.Common.SignalR;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Leagues;

public class LeaguesRefreshedEventHandler(ISignalRPublisher publisher)
    : IEventHandler<LeaguesRefreshedEvent>
{
    public async Task Handle(LeaguesRefreshedEvent notification, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(new LeaguesRefreshedDashboardEvent(), cancellationToken)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 7: Run integration tests**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --no-restore \
  --filter "FullyQualifiedName~LeaguesRefreshApiTests"
```
Expected: Both tests PASS.

- [ ] **Step 8: Run all tests and commit**

```bash
dotnet test --no-restore
git add src/Personal.Dashboard.Api.Host/Leagues/LeaguesRefreshedEventHandler.cs \
        src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesRefreshApiTests.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Support/PersonalDashboardApiApplication.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Personal.Dashboard.Api.Host.Tests.csproj
git commit -m "feat: add LeaguesRefreshedEventHandler and POST /leagues/refresh endpoint"
```

---

## Chunk 4: Scheduled Background Service

### Task 8: Add RefreshLeaguesBackgroundService

**Files:**
- Create: `src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesSettings.cs`
- Create: `src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesBackgroundService.cs`
- Modify: `src/Personal.Dashboard.Api.Host/PersonalDashboardApiServiceCollectionExtensions.cs`

- [ ] **Step 1: Create `RefreshLeaguesSettings`**

```csharp
// src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesSettings.cs
namespace Personal.Dashboard.Api.Host.Leagues;

public class RefreshLeaguesSettings
{
    public const string SectionName = "RefreshLeagues";
    public int IntervalHours { get; set; } = 24;
}
```

- [ ] **Step 2: Create `RefreshLeaguesBackgroundService`**

```csharp
// src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesBackgroundService.cs
using Microsoft.Extensions.Options;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Leagues.Commands;

namespace Personal.Dashboard.Api.Host.Leagues;

public class RefreshLeaguesBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshLeaguesSettings> settings,
    ILogger<RefreshLeaguesBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(settings.Value.IntervalHours);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<ICqrsBus>();
            try
            {
                await bus.ExecuteAsync(new RefreshLeaguesCommand());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled league refresh failed");
            }
        }
    }
}
```

- [ ] **Step 3: Register in `AddPersonalDashboardApi`**

```csharp
using Personal.Dashboard.Api.Host.Leagues;

services.AddOptions<RefreshLeaguesSettings>()
    .BindConfiguration(RefreshLeaguesSettings.SectionName);
services.AddHostedService<RefreshLeaguesBackgroundService>();
```

- [ ] **Step 4: Build, test, and commit**

```bash
dotnet build --no-restore && dotnet test --no-build --no-restore
git add src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesSettings.cs \
        src/Personal.Dashboard.Api.Host/Leagues/RefreshLeaguesBackgroundService.cs \
        src/Personal.Dashboard.Api.Host/PersonalDashboardApiServiceCollectionExtensions.cs
git commit -m "feat: add scheduled league refresh background service (default 24h interval)"
```

---

## Chunk 5: Blazor UI

### Task 9: Add IHubConnectionWrapper and IHubConnectionFactory

**Files:**
- Create: `src/Personal.Dashboard.Web.Host/Common/SignalR/IHubConnectionWrapper.cs`
- Modify: `src/Personal.Dashboard.Web.Host/PersonalDashboardWebServiceCollectionExtensions.cs`

`IHubConnectionWrapper.On` takes `Func<DashboardEvent, Task>` so async subscriber invocation flows correctly all the way through.

- [ ] **Step 1: Add `Microsoft.AspNetCore.SignalR.Client` to Web.Host**

```bash
dotnet add src/Personal.Dashboard.Web.Host \
  package Microsoft.AspNetCore.SignalR.Client
```

- [ ] **Step 2: Create the interfaces and implementations**

```csharp
// src/Personal.Dashboard.Web.Host/Common/SignalR/IHubConnectionWrapper.cs
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
```

- [ ] **Step 3: Register `IHubConnectionFactory` in `AddPersonalDashboardWeb`**

```csharp
using Personal.Dashboard.Web.Host.Common.SignalR;

services.AddSingleton<IHubConnectionFactory, HubConnectionFactory>();
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build src/Personal.Dashboard.Web.Host --no-restore
git add src/Personal.Dashboard.Web.Host/Common/SignalR/IHubConnectionWrapper.cs \
        src/Personal.Dashboard.Web.Host/PersonalDashboardWebServiceCollectionExtensions.cs \
        src/Personal.Dashboard.Web.Host/Personal.Dashboard.Web.Host.csproj
git commit -m "feat: add IHubConnectionWrapper and IHubConnectionFactory for Blazor SignalR"
```

---

### Task 10: Add FakeHubConnectionFactory and SetupRefreshLeagues to Test.Support

**Files:**
- Create: `tests/Personal.Dashboard.Test.Support/Common/SignalR/FakeHubConnectionFactory.cs`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardWebContext.cs`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs`

- [ ] **Step 1: Create `FakeHubConnectionWrapper` and `FakeHubConnectionFactory`**

`SimulateEventAsync` invokes all handlers in parallel via `Task.WhenAll`, matching the production code path. `SimulateEvent` is a fire-and-forget convenience for tests that use `Eventually.Assert`.

```csharp
// tests/Personal.Dashboard.Test.Support/Common/SignalR/FakeHubConnectionFactory.cs
using System.Collections.Concurrent;
using Personal.Dashboard.Models;
using Personal.Dashboard.Web.Host.Common.SignalR;

namespace Personal.Dashboard.Test.Support.Common.SignalR;

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
```

- [ ] **Step 2: Register `FakeHubConnectionFactory` in `PersonalDashboardWebContext`**

```csharp
// Add usings:
using Personal.Dashboard.Test.Support.Common.SignalR;
using Personal.Dashboard.Web.Host.Common.SignalR;

// Add to constructor after AddPersonalDashboardTestingServices():
var fakeFactory = new FakeHubConnectionFactory();
Services.ReplaceService<IHubConnectionFactory, FakeHubConnectionFactory>(_ => fakeFactory);

// Add property:
public FakeHubConnectionFactory HubFactory =>
    Services.GetRequiredService<FakeHubConnectionFactory>();
```

- [ ] **Step 3: Add `SetupRefreshLeagues` to `PersonalDashboardLeaguesApiExtensions`**

```csharp
// Add to PersonalDashboardLeaguesApiExtensions.cs:
public static async Task SetupRefreshLeagues(
    this FakeHttpMessageHandler handler,
    ConfigureResponseOptions? options = null)
{
    await handler.SetupResponseAsync(
        new HttpRequestMessage(HttpMethod.Post, "http://api/leagues/refresh"),
        new HttpResponseMessage(System.Net.HttpStatusCode.OK),
        options
    );
}
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build tests/ --no-restore
git add tests/Personal.Dashboard.Test.Support/Common/SignalR/FakeHubConnectionFactory.cs \
        tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardWebContext.cs \
        tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs
git commit -m "feat: add FakeHubConnectionFactory and SetupRefreshLeagues test helpers"
```

---

### Task 11: Update PersonalDashboardApiClient and Blazor UI (TDD)

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs`
- Modify: `src/Personal.Dashboard.Web.Host/App.razor`
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`:

```csharp
[Fact]
public async Task WhenRefreshButtonClickedThenCallsRefreshEndpoint()
{
    await using var context = new PersonalDashboardWebContext();
    HttpRequestMessage? refreshRequest = null;
    await context.HttpHandler.SetupLeagues();
    await context.HttpHandler.SetupRefreshLeagues(
        new ConfigureResponseOptions(Capture: req => refreshRequest = req)
    );

    var page = context.Render<LeaguesList>();
    await page.FindByRole("button", new FindByRoleOptions(Label: "refresh")).ClickAsync();

    await Eventually.Assert(() => Assert.NotNull(refreshRequest));
}

[Fact]
public async Task WhenLeaguesRefreshedEventReceivedThenRefetchesLeagues()
{
    await using var context = new PersonalDashboardWebContext();
    var refreshedLeague = DataFactory.FootballLeagueModel();
    await context.HttpHandler.SetupLeagues(leagues: [refreshedLeague]);

    var page = context.Render<LeaguesList>();
    await context.HubFactory.Connection.SimulateEventAsync(new LeaguesRefreshedDashboardEvent());

    await Eventually.Assert(() =>
        Assert.Contains(page.FindAll(".mud-list-item"),
            item => item.TextContent.Contains(refreshedLeague.Name)));
}

[Fact]
public async Task WhenLeagueHasLastRefreshedThenDisplaysIt()
{
    await using var context = new PersonalDashboardWebContext();
    var lastRefreshed = DateTimeOffset.UtcNow.AddHours(-2);
    var league = DataFactory.FootballLeagueModel() with { LastRefreshed = lastRefreshed };
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<LeaguesList>();

    await Eventually.Assert(() =>
        Assert.Contains(page.Markup, lastRefreshed.ToString("g")));
}
```

- [ ] **Step 2: Run to confirm new tests fail**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --no-restore \
  --filter "FullyQualifiedName~LeaguesListTests"
```
Expected: New tests FAIL.

- [ ] **Step 3: Update `PersonalDashboardApiClient`**

Both `Subscribe<TEvent>` overloads converge on the `Func<Task>` internal storage. `InvokeSubscribersAsync` runs all handlers in parallel via `Task.WhenAll`.

```csharp
// src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs
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

    // Action overload — wraps to Func<Task> and delegates to the Func<Task> overload
    public IDisposable Subscribe<TEvent>(Action handler) where TEvent : DashboardEvent, new()
        => Subscribe<TEvent>(() => { handler(); return Task.CompletedTask; });

    // Func<Task> overload — the single code path all subscriptions go through
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
```

- [ ] **Step 4: Update `App.razor`**

```razor
@inject Personal.Dashboard.Web.Host.Common.Apis.PersonalDashboardApiClient Client
@implements IAsyncDisposable

<Router AppAssembly="@typeof(App).Assembly" NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)"/>
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
</Router>

@code {
    protected override async Task OnInitializedAsync()
    {
        await Client.StartListeningAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Client.StopListeningAsync();
    }
}
```

- [ ] **Step 5: Update `LeaguesList.razor`**

Uses the `Func<Task>` overload directly — no fire-and-forget wrapper needed.

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis

@inject PersonalDashboardApiClient Client
@implements IAsyncDisposable

<MudGrid>
    <MudItem xs="12">
        <MudStack Row="true" AlignItems="AlignItems.Center">
            <MudText Typo="Typo.h6">Leagues</MudText>
            <MudIconButton Icon="@Icons.Material.Rounded.Refresh"
                           OnClick="@OnRefreshClicked"
                           role="button"
                           aria-label="refresh" />
        </MudStack>
    </MudItem>
    <MudItem xs="12">
        <MudPaper Class="d-flex flex-1">
            <MudList Class="d-flex flex-1" T="FootballLeagueModel">
                @foreach (var league in Leagues.Items)
                {
                    <MudListItem>
                        <MudText Typo="Typo.body1">@league.Name</MudText>
                        @if (league.LastRefreshed.HasValue)
                        {
                            <MudText Typo="Typo.caption">@league.LastRefreshed.Value.ToString("g")</MudText>
                        }
                    </MudListItem>
                }
            </MudList>
            <MudStack>
                <MudIconButton Icon="@Icons.Material.Rounded.NavigateBefore"
                               OnClick="@GoToPrevious"
                               role="button"
                               aria-label="previous" />
                <MudIconButton Icon="@Icons.Material.Rounded.NavigateNext"
                               OnClick="@GoToNext"
                               Disabled="@(!HasNextPage)"
                               role="button"
                               aria-label="next" />
            </MudStack>
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private PagedListResultModel<FootballLeagueModel> Leagues { get; set; } =
        PagedListResultModel<FootballLeagueModel>.Empty();

    private bool HasNextPage => Leagues.Total > Leagues.Limit;
    private IDisposable? _subscription;
    private PagedListParameters _currentParameters = PagedListParameters.Default();

    protected override async Task OnInitializedAsync()
    {
        _subscription = Client.Subscribe<LeaguesRefreshedDashboardEvent>(async () =>
        {
            await LoadLeaguesAsync();
            StateHasChanged();
        });
        await LoadLeaguesAsync();
    }

    private async Task LoadLeaguesAsync()
    {
        Leagues = await Client.GetLeaguesAsync(_currentParameters);
    }

    private async Task OnRefreshClicked()
    {
        await Client.RefreshLeaguesAsync();
    }

    private async Task GoToNext()
    {
        _currentParameters = new PagedListParameters(
            offset: _currentParameters.Offset + _currentParameters.Limit);
        await LoadLeaguesAsync();
        StateHasChanged();
    }

    private async Task GoToPrevious()
    {
        _currentParameters = new PagedListParameters(
            offset: Math.Max(0, _currentParameters.Offset - _currentParameters.Limit));
        await LoadLeaguesAsync();
        StateHasChanged();
    }

    public ValueTask DisposeAsync()
    {
        _subscription?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 6: Run all Blazor tests**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --no-restore
```
Expected: All tests pass.

- [ ] **Step 7: Run all tests**

```bash
dotnet test --no-restore
```
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs \
        src/Personal.Dashboard.Web.Host/App.razor \
        src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs
git commit -m "feat: add refresh button, LastRefreshed display, and SignalR auto-update to leagues UI"
```

---

## Final Verification

- [ ] **Run the full test suite in Release config**

```bash
dotnet test --configuration Release --no-restore
```
Expected: All tests pass.

- [ ] **Build in Release**

```bash
dotnet build --configuration Release --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Add the plan to the solution file**

In `Personal.Dashboard.slnx`, add to the `/.docs/` folder:
```xml
<File Path="docs/superpowers/plans/2026-03-10-leagues-refresh.md" />
```

- [ ] **Final commit**

```bash
git add Personal.Dashboard.slnx docs/superpowers/plans/2026-03-10-leagues-refresh.md
git commit -m "docs: add leagues refresh implementation plan to solution"
```
