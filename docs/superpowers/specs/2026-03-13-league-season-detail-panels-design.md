# League Seasons & Detail Panels Design

**Goal:** Add `FootballLeagueSeason` entity to properly track seasons per league, require explicit league+season params in `RefreshClubsCommand`, sort favorites to the top of lists, and add detail panels to both the Leagues and Clubs pages.

**Architecture:** A new `FootballLeagueSeason` entity replaces the `CurrentSeasonYear` scalar on `FootballLeagueEntity`. Seasons are populated from the Football API's `Seasons[]` array during league refresh. Favoriting a league still triggers club loading, but now uses the correct current season from the DB rather than guessing the calendar year. Detail panels are read-only Blazor components wired via a selected-item callback pattern.

**Tech Stack:** .NET 10, EF Core, MediatR CQRS, Blazor WASM, MudBlazor, AutoMapper, Football API (api-sports.com)

---

## Section 1: Data Model

### New entity: `FootballLeagueSeason`

Location: `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueSeason.cs` (co-located with `FootballLeagueEntityConfiguration`)

```csharp
public class FootballLeagueSeason
{
    public Guid LeagueId { get; set; }
    public int Year { get; set; }
    public bool IsCurrent { get; set; }
    public FootballLeagueEntity League { get; set; } = null!;
}

public class FootballLeagueSeasonConfiguration : IEntityTypeConfiguration<FootballLeagueSeason>
{
    public void Configure(EntityTypeBuilder<FootballLeagueSeason> builder)
    {
        builder.HasKey(s => new { s.LeagueId, s.Year });
        builder.HasOne(s => s.League)
            .WithMany(l => l.Seasons)
            .HasForeignKey(s => s.LeagueId);
    }
}
```

### Updated `FootballLeagueEntity`

- **Remove** `CurrentSeasonYear` property
- **Add** `ICollection<FootballLeagueSeason> Seasons` navigation property
- `UpdateFromFootballApi` no longer sets `CurrentSeasonYear`; season upsert is handled by `RefreshLeaguesCommandHandler`

### Migration

Single migration: drops `CurrentSeasonYear` column from `FootballLeagueEntity`, creates `FootballLeagueSeasons` table with composite PK `(LeagueId, Year)`.

### Updated shared models (`src/Personal.Dashboard.Models/FootballModels.cs`)

```csharp
public record FootballLeagueSeasonModel(int Year, bool IsCurrent);

public record FootballLeagueModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballLeagueSeasonModel[] Seasons);

public record FootballClubLeagueModel(Guid Id, string Name);

public record FootballClubModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballClubLeagueModel[] Leagues);
```

### AutoMapper profiles

- `LeaguesMappingProfile`: map `FootballLeagueSeason` → `FootballLeagueSeasonModel`, map `FootballLeagueEntity` → `FootballLeagueModel` (includes `Seasons`)
- `ClubsMappingProfile`: map `FootballClubEntity.Leagues` → `FootballClubLeagueModel[]`

---

## Section 2: Backend Commands & Queries

### `RefreshLeaguesCommandHandler`

After upserting each league entity, upsert its seasons:

```csharp
// For each apiLeague, after updating the entity:
var existingSeasons = entity.Seasons.ToDictionary(s => s.Year);
foreach (var apiSeason in apiLeague.Seasons)
{
    var year = (int)apiSeason.Year;
    if (existingSeasons.TryGetValue(year, out var season))
        season.IsCurrent = apiSeason.Current;
    else
        entity.Seasons.Add(new FootballLeagueSeason { Year = year, IsCurrent = apiSeason.Current });
}
```

After `SaveChangesAsync`, dispatch `RefreshClubsCommand` for each favorited league that has a current season:

```csharp
var favoritedWithCurrentSeason = context.Set<FootballLeagueEntity>()
    .Where(l => l.IsFavorite)
    .Include(l => l.Seasons)
    .AsEnumerable()
    .Select(l => (l, l.Seasons.FirstOrDefault(s => s.IsCurrent)))
    .Where(t => t.Item2 is not null);

foreach (var (league, season) in favoritedWithCurrentSeason)
    await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, season!.Year), cancellationToken);
```

### `RefreshClubsCommand` — both params required

```csharp
public record RefreshClubsCommand(Guid LeagueId, int SeasonYear) : ICommand;
```

- Removes `HandleAllLeagues` variant entirely
- `HandleWithLeague` uses `SeasonYear` directly — no calendar-year fallback
- Fetches the league with aliases, calls `GetTeamsAsync(League: faAlias, Season: SeasonYear)`

### `FavoriteLeagueCommandHandler`

Loads the current season from the league's seasons, dispatches `RefreshClubsCommand` if found:

```csharp
var league = await db.Set<FootballLeagueEntity>()
    .Include(l => l.Seasons)
    .FirstOrDefaultAsync(l => l.Id == request.LeagueId, cancellationToken)
    ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);

league.Favorite();
await db.SaveChangesAsync(cancellationToken);

var currentSeason = league.Seasons.FirstOrDefault(s => s.IsCurrent);
if (currentSeason is null)
{
    logger.LogWarning("League {LeagueId} has no current season; clubs not loaded", request.LeagueId);
    return;
}
try
{
    await bus.ExecuteAsync(new RefreshClubsCommand(request.LeagueId, currentSeason.Year), cancellationToken);
}
catch (Exception e)
{
    logger.LogWarning(e, "Failed to refresh clubs for league {LeagueId}", request.LeagueId);
}
```

### `GetLeaguesQuery`

```csharp
var query = context.Set<FootballLeagueEntity>()
    .Include(l => l.Seasons)
    .OrderByDescending(l => l.IsFavorite)
    .ThenBy(l => l.Name)
    .ProjectTo<FootballLeagueModel>(mapper.ConfigurationProvider);
```

### `GetClubsQuery`

```csharp
var query = context.Set<FootballClubEntity>()
    .Include(c => c.Leagues)
    .OrderByDescending(c => c.IsFavorite)
    .ThenBy(c => c.Name)
    .ProjectTo<FootballClubModel>(mapper.ConfigurationProvider);
```

---

## Section 3: API Endpoints

No new endpoints. Changes to existing behavior:

**`POST /clubs/refresh`** — iterates favorited leagues and dispatches individual commands:

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshClubs()
{
    var leagues = await db.Set<FootballLeagueEntity>()
        .Where(l => l.IsFavorite)
        .Include(l => l.Seasons)
        .ToListAsync();

    foreach (var league in leagues)
    {
        var current = league.Seasons.FirstOrDefault(s => s.IsCurrent);
        if (current is not null)
            await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, current.Year));
    }
    return NoContent();
}
```

Note: `ClubsController` needs `PersonalDashboardContext` injected for this query, or this logic moves into a new `RefreshAllFavoritedClubsCommand`. Keep it in a dedicated command to follow the CQRS pattern:

```csharp
public record RefreshAllFavoritedClubsCommand : ICommand;
// Handler: queries favorited leagues with current seasons,
// dispatches RefreshClubsCommand for each
```

**All other endpoints** (`GET /leagues`, `GET /clubs`, favorite/unfavorite) are unchanged in signature — they return richer models automatically via AutoMapper.

---

## Section 4: UI Components

### `Leagues.razor`

```razor
@page "/leagues"
<MudGrid>
  <MudItem xs="3">
    <LeaguesList OnLeagueSelected="@(l => _selected = l)" />
  </MudItem>
  <MudItem xs="9">
    <LeagueDetail League="_selected" />
  </MudItem>
</MudGrid>
@code { private FootballLeagueModel? _selected; }
```

### `LeaguesList` updates

- Show current season year next to each league name: `(Seasons.FirstOrDefault(s => s.IsCurrent)?.Year?.ToString() ?? "")`
- Row becomes clickable (wraps in `MudListItem` with `OnClick` invoking `OnLeagueSelected` callback)
- `[Parameter] public EventCallback<FootballLeagueModel> OnLeagueSelected`
- Favorite toggle stays in the row; clicking it does NOT propagate selection change

### `LeagueDetail` (new component, `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor`)

- `[Parameter] public FootballLeagueModel? League`
- When `League` is null: shows placeholder text "Select a league to view details"
- When set:
  - League name (heading) + LastRefreshed caption
  - Favorite toggle button (calls `FavoriteLeagueAsync` / `UnfavoriteLeagueAsync`, reloads via callback)
  - "Current Season" card: displays `{Year} – {Year+1}` label, no interactive button
  - Seasons list: all seasons from `League.Seasons` ordered by Year descending, current one marked with a chip/badge

### `Clubs.razor`

```razor
@page "/clubs"
<MudGrid>
  <MudItem xs="3">
    <ClubsList OnClubSelected="@(c => _selected = c)" />
  </MudItem>
  <MudItem xs="9">
    <ClubDetail Club="_selected" />
  </MudItem>
</MudGrid>
@code { private FootballClubModel? _selected; }
```

### `ClubsList` updates

- Row becomes clickable → invokes `OnClubSelected` callback
- `[Parameter] public EventCallback<FootballClubModel> OnClubSelected`
- Favorite toggle stays in the row

### `ClubDetail` (new component, `src/Personal.Dashboard.Web.Host/Clubs/ClubDetail.razor`)

- `[Parameter] public FootballClubModel? Club`
- When `Club` is null: "Select a club to view details"
- When set:
  - Club name + LastRefreshed
  - Favorite toggle
  - "Leagues" section: list of `Club.Leagues` names (read-only)

### API client additions (`PersonalDashboardApiClient`)

No new methods — existing `FavoriteLeagueAsync`/`UnfavoriteLeagueAsync` and `FavoriteClubAsync`/`UnfavoriteClubAsync` are sufficient. Detail components call these same endpoints.

---

## Testing

### Core unit tests

- `RefreshLeaguesCommandTests`: verify seasons are upserted correctly, `IsCurrent` updated
- `RefreshClubsCommandTests`: verify `(LeagueId, SeasonYear)` both required; API called with correct params
- `FavoriteLeagueCommandTests`: verify `RefreshClubsCommand` dispatched with current season year; no dispatch when no current season

### API integration tests

- `LeaguesControllerTests`: `GET /leagues` returns `Seasons[]` on model; favorites sorted first
- `ClubsControllerTests`: `GET /clubs` returns `Leagues[]` on model; favorites sorted first; `POST /clubs/refresh` dispatches per-league commands

### Blazor component tests

- `LeaguesListTests`: clicking a row invokes `OnLeagueSelected` with correct model; current season year displayed
- `LeagueDetailTests`: shows placeholder when null; renders season list; favorite toggle works
- `ClubsListTests`: clicking a row invokes `OnClubSelected`
- `ClubDetailTests`: shows placeholder when null; renders leagues list

### Feature tests (Playwright)

- Update `WhenLeagueFavoritedThenClubsAppearInClubsList`: clicking league row → detail panel shows seasons
- Update `WhenClubFavoritedThenShowsAsFavorite`: clubs page shows club detail on click
- Existing favorite toggle tests remain valid
