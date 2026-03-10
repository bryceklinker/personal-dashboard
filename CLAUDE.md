# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore tools and packages
dotnet tool restore
dotnet restore

# Build
dotnet build --configuration Release --no-restore

# Run all tests
dotnet test --configuration Release --no-build --no-restore

# Run a single test project
dotnet test tests/Personal.Dashboard.Core.Tests

# Run (Aspire orchestrates all services including PostgreSQL)
dotnet run --project src/Personal.Dashboard.Host

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```

## Architecture

This is a .NET 10 personal dashboard using .NET Aspire for service orchestration.

**Service topology** (defined in `src/Personal.Dashboard.Host/AppHost.cs`):
- PostgreSQL container → Migrations Worker → REST API → Blazor WASM client
- Aspire handles service discovery, connection strings, and startup ordering

**Projects:**

| Project | Role |
|---|---|
| `Personal.Dashboard.Host` | Aspire AppHost — orchestrates all services |
| `Personal.Dashboard.Api.Host` | ASP.NET Core REST API |
| `Personal.Dashboard.Web.Host` | Blazor WebAssembly client (MudBlazor UI) |
| `Personal.Dashboard.Migrations.Host` | EF Core migrations worker (runs at startup) |
| `Personal.Dashboard.Core` | Business logic, CQRS handlers, EF Core DbContext |
| `Personal.Dashboard.Models` | Shared DTOs between API and client |
| `Personal.Dashboard.Aspire.Defaults` | Shared Aspire/OpenTelemetry configuration |

**CQRS pattern** (implemented in `Personal.Dashboard.Core`):
- Commands and queries use MediatR with `ICommand`/`IQuery<T>` interfaces
- Events use MediatR notifications with `IEvent` / `IEventHandler<T>` / `IEventBus`
- `ICqrsBus` extends both `ICommandBus` and `IQueryBus` and also exposes `PublishAsync<TEvent>`
- `CqrsBus` / `CommandBus` / `QueryBus` wrap MediatR
- MediatR pipeline behaviors: `CqrsValidationPipelineBehavior` (FluentValidation) and `CqrsLoggingBehavior`
- Controllers extend `CqrsController` which injects the bus

**Adding a new feature** follows the pattern in `src/Personal.Dashboard.Core/Leagues/`:
1. Entity + co-located `IEntityTypeConfiguration<T>` in `/Entities`
2. Query handlers in `/Queries`, command handlers in `/Commands`
3. Event definitions in `/Events` (implement `IEvent`; handlers live in the host layer)
4. AutoMapper profile in `/Mappers`
5. `DataSource.cs` constants for alias sources (e.g. `FootballApi`) if the entity has aliases
6. API controller in `src/Personal.Dashboard.Api.Host/<Feature>/`
7. Blazor components in `src/Personal.Dashboard.Web.Host/<Feature>/`

**Testing — three tiers:**

| Tier | Project | Bootstrap |
|------|---------|-----------|
| Core unit tests | `Personal.Dashboard.Core.Tests` | `PersonalDashboardCoreTestingProviderFactory.Create()` |
| API integration tests | `Personal.Dashboard.Api.Host.Tests` | `PersonalDashboardApiApplication` (WebApplicationFactory) |
| Blazor component tests | `Personal.Dashboard.Web.Host.Tests` | `PersonalDashboardWebContext` (bUnit) |

Shared test helpers (`Personal.Dashboard.Test.Support`):
- `DataFactory` / `PersonalDashboardEntityFactory` / `FootballApiDataFactory` — fake data builders (Bogus)
- `FakeHttpMessageHandler` + domain-specific `SetupX` extensions — mock HTTP responses
- `FakeHubConnectionFactory` — fake SignalR hub for Web component tests
- `CapturingCqrsBus` — wraps `ICqrsBus`; assert which commands/queries/events were dispatched
- `Eventually` — polls an assertion up to 5 s with 200 ms delay for async UI assertions

Test naming convention: `WhenX ThenY` (e.g. `WhenNoLeaguesExistThenReturnsEmptyResult`)
