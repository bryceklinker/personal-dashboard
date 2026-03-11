# Sub-Spec 1: League Favoriting + Clubs

**Date:** 2026-03-10
**Series:** Tracking & Dashboard (1 of 3)

## Overview

Add the ability to mark leagues and clubs as favorites. Favoriting a league triggers an initial refresh of clubs for that league. Clubs are a new top-level domain entity with their own CQRS structure, mirroring leagues.

## Data Model

### FootballLeagueEntity changes
- Add `IsFavorite` (bool, default `false`)
- Add `Clubs` navigation property (many-to-many with `FootballClubEntity`)
- Add `Favorite()` and `Unfavorite()` methods
- Add `UpdateFromFootballApi(FootballApiLeague league)` method — the parameter is the full `FootballApiLeague` record; the method internally reads `league.League.Name` to set `Name` and sets `LastRefreshed` to `DateTimeOffset.UtcNow`. The existing `RefreshLeaguesCommandHandler` is updated to call this method instead of directly assigning properties.

### FootballClubEntity (new)
Located in `Core/Clubs/Entities/`:
- `Id` (Guid), `Name` (string), `IsFavorite` (bool), `LastRefreshed` (DateTimeOffset?)
- `Leagues` — many-to-many collection of `FootballLeagueEntity`
- `Aliases` — collection of `FootballClubAlias`
- Methods:
  - `AddAlias(source, alias)` — appends a new `FootballClubAlias` to `Aliases`
  - `AddLeague(league)` — appends to `Leagues`
  - `Favorite()` / `Unfavorite()` — sets `IsFavorite`
  - `UpdateFromFootballApi(FootballApiTeam team)` — sets `Name` to `team.Team.Name`, sets `LastRefreshed` to `DateTimeOffset.UtcNow`; called during `RefreshClubsCommand` for both new and existing clubs

### FootballClubAlias (new)
`FootballClubAlias` is its own separate file `FootballClubAlias.cs` in `Core/Clubs/Entities/`, mirroring the `FootballLeagueAlias.cs` pattern exactly:
- No surrogate `Id` — primary key is composite `(AliasSource, Alias, ClubId)`, configured in a co-located `FootballClubAliasConfiguration : IEntityTypeConfiguration<FootballClubAlias>`
- `AliasSource` (string), `Alias` (string)
- `ClubId` (Guid, FK to `FootballClubEntity`)
- `Club` (required navigation property back to `FootballClubEntity`)

### DataSource constants
`DataSource.FootballApi` in `Core/Common/DataSource.cs` is already defined and is reused for club aliases. No new constants or files are needed.

### EF Configuration
`FootballClubEntityConfiguration` (co-located in `FootballClubEntity.cs`) configures:
- PK, alias composite key, and alias FK — same pattern as `FootballLeagueEntityConfiguration`
- Many-to-many between `FootballClubEntity` and `FootballLeagueEntity` with explicit join table name `FootballLeagueClub`, configured here only. `FootballLeagueEntityConfiguration` is not modified; EF discovers the `Clubs` navigation property on `FootballLeagueEntity` automatically from the configuration on the club side.

### EF Migrations
Two separate `dotnet ef migrations add` invocations, in this order:
1. `AddIsFavoriteToLeague` — adds `IsFavorite` column to `FootballLeague` table
2. `AddFootballClub` — adds `FootballClub`, `FootballClubAlias`, and `FootballLeagueClub` join tables

### Models
- `FootballClubModel` added to `Personal.Dashboard.Models`: `Id`, `Name`, `IsFavorite`, `LastRefreshed`
- `FootballLeagueModel` gains `IsFavorite`

### SignalR Dashboard Events
`ClubsRefreshedDashboardEvent` — a parameterless record added to `DashboardEvents.cs` in `Personal.Dashboard.Models`, mirroring `LeaguesRefreshedDashboardEvent`.

## Football API Integration

New method on `IFootballApiClient` and `FootballApiClient`:

```csharp
Task<FootballApiResponse<FootballApiTeamsParameters, FootballApiTeam[]>> GetTeamsAsync(
    FootballApiTeamsParameters parameters
);
```

New models in `FootballApiModels.cs`:
- `FootballApiTeamInfo(long Id, string Name)` — `Id` is `long`, consistent with `FootballApiLeagueInfo.Id`. Only `Id` and `Name` are mapped; additional fields (logo, country, venue, etc.) are intentionally omitted. No `[JsonPropertyName]` attributes needed — follows the same case-insensitive deserialization pattern as existing models.
- `FootballApiTeam(FootballApiTeamInfo Team)` — mirrors `FootballApiLeague(FootballApiLeagueInfo League, ...)`; no `[JsonPropertyName]` needed.
- `FootballApiTeamsParameters` — follows the same dictionary-initializer pattern as `FootballApiLeaguesParameters`:

```csharp
public record FootballApiTeamsParameters(
    long? League = null,
    long? Season = null
) : FootballApiParameters(new Dictionary<string, object?>
{
    { "league", League },
    { "season", Season }
});
```

When refreshing clubs, the league's `FootballApi` alias provides the `league` parameter. `DateTimeOffset.UtcNow.Year` is used as the `season`.

## CQRS

### Clubs feature structure
```
Core/Clubs/
  Commands/
    FavoriteClubCommand.cs
    RefreshClubsCommand.cs
    UnfavoriteClubCommand.cs
  Entities/
    FootballClubAlias.cs
    FootballClubEntity.cs
  Events/
    ClubsRefreshedEvent.cs
  Mappers/
    FootballClubMapper.cs
  Queries/
    GetClubsQuery.cs
```

### League commands additions (in `Core/Leagues/Commands/`)
```
Core/Leagues/Commands/
  FavoriteLeagueCommand.cs    (new)
  RefreshLeaguesCommand.cs    (existing, updated)
  UnfavoriteLeagueCommand.cs  (new)
```

### Commands

**`FavoriteClubCommand(Guid ClubId)`** — loads the club, calls `Favorite()`, saves.

**`UnfavoriteClubCommand(Guid ClubId)`** — loads the club, calls `Unfavorite()`, saves.

**`RefreshClubsCommand(Guid? LeagueId = null)`**:

- **With LeagueId** — looks up the league's `FootballApi` alias to get the FA league ID, calls `GetTeamsAsync`. Loads existing `FootballClubAlias` rows where `AliasSource == DataSource.FootballApi` into a dictionary keyed by `Alias` string. For each `FootballApiTeam` in the response: if `team.Team.Id.ToString()` exists in the dictionary, call `UpdateFromFootballApi` on the existing club; otherwise create a new `FootballClubEntity`, call `AddAlias(DataSource.FootballApi, team.Team.Id.ToString())`, `AddLeague(league)`, and `UpdateFromFootballApi`. Saves, then publishes `ClubsRefreshedEvent`. Used by `FavoriteLeagueCommand`.

- **Without LeagueId** — loads all `FootballClubAlias` rows where `AliasSource == DataSource.FootballApi`, with `Include(a => a.Club).ThenInclude(c => c.Leagues).ThenInclude(l => l.Aliases)` so each league's own `FootballApi` alias is available without additional queries. Groups clubs by distinct league. For each league, looks up that league's `FootballApi` alias from the already-loaded `l.Aliases` collection, calls `GetTeamsAsync`, and matches results to clubs using the alias dictionary. Calls `UpdateFromFootballApi` on matched clubs. Clubs with no matching FA response entry are left unchanged. Skips leagues with no `FootballApi` alias. Saves, then publishes `ClubsRefreshedEvent`. If no clubs exist this is a no-op.

**`FavoriteLeagueCommand(Guid LeagueId)`** — loads the league, calls `Favorite()`, calls `SaveChangesAsync` (first commit — favorite is persisted). Then dispatches `RefreshClubsCommand(LeagueId)`. If the club refresh fails, the favorite is preserved because it was already committed in a separate `SaveChangesAsync` call. The error propagates to the caller (the API endpoint returns a 500).

**`UnfavoriteLeagueCommand(Guid LeagueId)`** — loads the league, calls `Unfavorite()`, saves.

**`RefreshLeaguesCommand` update** — handler updated to call `league.UpdateFromFootballApi(apiLeague)` instead of directly mutating `Name` and `LastRefreshed`. After saving leagues, unconditionally dispatches `RefreshClubsCommand()` (no LeagueId). The no-LeagueId path is a no-op when no clubs exist.

### Queries
- `GetClubsQuery(int Offset, int Limit)` inherits `PagedQuery<FootballClubModel>` — same pattern as `GetLeaguesQuery`. Returns a paged list of all clubs mapped to `FootballClubModel` using `ToPagedListAsync`.

### Events
- `ClubsRefreshedEvent` implementing `IEvent`

### Mappers
- `FootballClubMapper` profile: `FootballClubEntity` → `FootballClubModel`
- Update `FootballLeagueMapper` to include `IsFavorite`

## API Endpoints

### LeaguesController additions
- `POST /leagues/{id}/favorite` → `FavoriteLeagueCommand` → `204 No Content`
- `POST /leagues/{id}/unfavorite` → `UnfavoriteLeagueCommand` → `204 No Content`

### ClubsController (new, `Api.Host/Clubs/`)
- `GET /clubs?offset={n}&limit={n}` → `GetClubsQuery` → `200 OK` with `PagedListResultModel<FootballClubModel>`
- `POST /clubs/refresh` (no body) → `RefreshClubsCommand()` → `204 No Content`
- `POST /clubs/{id}/favorite` → `FavoriteClubCommand` → `204 No Content`
- `POST /clubs/{id}/unfavorite` → `UnfavoriteClubCommand` → `204 No Content`

### SignalR
`ClubsRefreshedEventHandler` in `Api.Host/Clubs/` — subscribes to `ClubsRefreshedEvent`, publishes `ClubsRefreshedDashboardEvent` to the hub. Follows the same pattern as `LeaguesRefreshedEventHandler`.

## UI

### Leagues list update
Add a favorite toggle (star icon button) per item in `LeaguesList.razor`. Clicking calls the favorite/unfavorite endpoint. On success, reload the list. On failure, display a MudBlazor snackbar error and leave the toggle in its previous state.

### New Clubs page
```
Web.Host/Clubs/
  Clubs.razor       (@page "/clubs")
  ClubsList.razor
```

`Clubs.razor` — same grid layout as `Leagues.razor`: `ClubsList` in left panel (xs=3), right panel empty (reserved for detail view in a future sub-spec).

`ClubsList.razor` — paged list of all clubs:
- Header with "Clubs" label and refresh icon button
- Each item shows name, `LastRefreshed` formatted as `"g"` (matching `LeaguesList.razor`), and favorite toggle (star icon). On favorite/unfavorite failure, display a snackbar error and revert the toggle.
- Empty state: if no clubs are loaded, display a message indicating clubs appear after favoriting a league
- Prev/next pagination
- Subscribes to `ClubsRefreshedDashboardEvent` via SignalR for auto-refresh

### API client additions
New methods on `PersonalDashboardApiClient`, all returning `Task` and throwing on non-success HTTP status (matching the existing `RefreshLeaguesAsync` pattern):
- `GetClubsAsync(PagedListParameters)` — returns `PagedListResultModel<FootballClubModel>`
- `RefreshClubsAsync()`
- `FavoriteClubAsync(Guid id)`
- `UnfavoriteClubAsync(Guid id)`
- `FavoriteLeagueAsync(Guid id)`
- `UnfavoriteLeagueAsync(Guid id)`

### Navigation
Add Clubs entry to nav menu.

## Testing

### Unit tests (`Personal.Dashboard.Core.Tests`)
Handler tests for all commands and queries using `PersonalDashboardCoreTestingProviderFactory`. Naming: `WhenX ThenY`.

### API integration tests (`Personal.Dashboard.Api.Host.Tests`)
Endpoint tests for all new and modified endpoints via `PersonalDashboardApiApplication`.

### Blazor component tests (`Personal.Dashboard.Web.Host.Tests`)
Component tests for `ClubsList` and the updated `LeaguesList` (favorite toggle) via `PersonalDashboardWebContext` with `FakeHubConnectionFactory`.

### Feature tests (`Personal.Dashboard.Feature.Tests`) — new project

Located under `tests/Personal.Dashboard.Feature.Tests/`.

`Directory.Build.props` automatically provides `TargetFramework`, `ImplicitUsings`, `Nullable`, `IsTestProject`, and the xunit/coverlet packages for any project whose name contains "Tests". After the project is created (e.g., via `dotnet new xunit`), remove all properties and package references from the `.csproj` that are already supplied by `Directory.Build.props`, keeping only packages and references unique to this project.

**`.csproj` contents after deduplication:**
```xml
<ItemGroup>
  <PackageReference Include="Aspire.Hosting.Testing" Version="..." />
  <PackageReference Include="Microsoft.Playwright" Version="..." />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\Personal.Dashboard.Host\Personal.Dashboard.Host.csproj" />
</ItemGroup>
```

**Project structure:**
```
tests/Personal.Dashboard.Feature.Tests/
  Fixtures/
    ApplicationFixture.cs   — starts the full application stack, shared once per test run
    PlaywrightFixture.cs    — launches Chromium, provides IPage per test class
  Leagues/
    LeagueFavoritingTests.cs
  Clubs/
    ClubFavoritingTests.cs
```

**`ApplicationFixture`** — shared via xUnit `ICollectionFixture` so the full application stack starts once per test run (not per class, as startup is expensive):
- Implements `IAsyncLifetime`
- `InitializeAsync` — starts the full application stack using `DistributedApplicationTestingBuilder`
- Exposes `GetEndpoint(resourceName)` to retrieve the running URL for a named resource (e.g., `"webfrontend"` for the Blazor app)
- `DisposeAsync` — shuts down the application
- A `[CollectionDefinition("Application")]` class and `[Collection("Application")]` attribute are used by tests that depend on this fixture

**`PlaywrightFixture`** implements `IAsyncLifetime` via `IClassFixture` (per test class):
- `InitializeAsync` — launches Chromium
- Exposes `NewPageAsync()` to get a fresh `IPage`
- `DisposeAsync` — disposes the browser

Tests are decorated with `[Collection("Application")]` for the shared application fixture and `IClassFixture<PlaywrightFixture>` for the browser. Each test navigates to the Blazor app URL from `ApplicationFixture` and interacts via Playwright selectors.

**CI note:** Add a `playwright install chromium` step in CI before running feature tests.

**Scenarios covered in this sub-spec:**
- Viewing the leagues list and toggling a league as favorite
- Clubs appearing in the clubs list after a league is favorited
- Favoriting a club from the clubs list
