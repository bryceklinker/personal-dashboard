# League Country Design

## Goal

Store and display country information (name, code, flag) for each football league, using a normalised `FootballCountry` entity that supports multiple external data sources via aliases — the same pattern used for leagues and clubs.

## Architecture

Introduce a `FootballCountry` entity that leagues belong to. Countries are upserted during `RefreshLeaguesCommand` by looking up their Football API alias (country name), creating a new country if one is not found. The league entity gains a `CountryId` FK. The shared model gains a nested `FootballCountryModel`. The league list and detail UI display the flag image and country name.

## Tech Stack

.NET 10, EF Core (PostgreSQL/Npgsql), AutoMapper, Blazor WASM, MudBlazor

---

## Data Layer

### `FootballCountry` entity

```
Id          Guid (PK, client-generated via Guid.NewGuid())
Name        string (required)
Code        string? (nullable — "World" and some international competitions have no code)
Flag        string (required — URL to flag SVG from Football API)
Aliases     ICollection<FootballCountryAlias>
Leagues     ICollection<FootballLeagueEntity>
```

Follows the same encapsulation pattern as `FootballLeagueEntity`:
- `CreateFromFootballApi(FootballApiCountry)` — static factory; adds Football API alias, sets name/code/flag
- `UpdateFromFootballApi(FootballApiCountry)` — updates name, code, flag
- `AddAlias(string source, string alias)` — public, for test setup and future sources

### `FootballCountryAlias` entity

```
AliasSource  string  (part of composite PK)
Alias        string  (part of composite PK)
CountryId    Guid    (part of composite PK, FK → FootballCountry.Id)
Country      FootballCountry (required navigation)
```

For the Football API source (`DataSource.FootballApi`), the alias value is the country **Name** (e.g. `"England"`). The Football API has no numeric country ID, and Name is always present.

### `FootballLeagueEntity` change

Add `CountryId` (Guid, nullable FK → `FootballCountry.Id`) and `Country` (`FootballCountry?` navigation). Nullable because existing rows have no country data until the next refresh.

---

## Command Changes

### `RefreshLeaguesCommandHandler`

At the start of `Handle`, load existing country aliases (Football API source) into a `Dictionary<string, FootballCountry>` keyed by alias, alongside the existing league alias dict.

For each API league, resolve the country:
- If the country alias exists → call `country.UpdateFromFootballApi(apiLeague.Country)`
- If not → `FootballCountry.CreateFromFootballApi(apiLeague.Country)`, add to context

Pass the resolved country into `FootballLeagueEntity.CreateFromFootballApi` / `UpdateFromFootballApi` so the league's `Country` navigation is set.

`FootballLeagueEntity.CreateFromFootballApi` and `UpdateFromFootballApi` both receive a `FootballCountry` parameter and set the navigation.

---

## Query Changes

### `GetLeaguesQuery`

Add `.Include(l => l.Country)` so the country navigation is populated for projection.

---

## Shared Model

```csharp
public record FootballCountryModel(string Name, string? Code, string Flag);

public record FootballLeagueModel(
    Guid Id,
    string Name,
    FootballCountryModel? Country,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballLeagueSeasonModel[] Seasons);
```

`Country` is nullable on the model to handle leagues refreshed before country data existed.

---

## Mapping

`LeaguesMappingProfile` adds:
```csharp
CreateMap<FootballCountry, FootballCountryModel>();
```

`FootballLeagueEntity → FootballLeagueModel` gains `.ForMember(d => d.Country, o => o.MapFrom(s => s.Country))`.

---

## Migration

One migration: `AddFootballCountry`
- Creates `FootballCountry` table (Id, Name, Code, Flag)
- Creates `FootballCountryAlias` table (AliasSource, Alias, CountryId composite PK + FK)
- Adds `CountryId` nullable FK column to `FootballLeagueEntity`

---

## UI

### `LeaguesList.razor`

Each list row displays:
- Flag: `<img src="@league.Country?.Flag" style="height:20px" />` (hidden if Country is null)
- Country name as a caption beneath the league name

### `LeagueDetail.razor`

Detail panel adds a country row below the league title:
- Flag image + country name and code (e.g. "England · GB")

---

## Testing

### Core unit tests
- `FootballCountry.CreateFromFootballApi` stores alias, name, code, flag
- `FootballCountry.UpdateFromFootballApi` updates name, code, flag
- `RefreshLeaguesCommand` creates new countries and assigns them to leagues
- `RefreshLeaguesCommand` reuses existing countries on subsequent refresh

### API integration tests
- `GET /leagues` returns `country.name`, `country.code`, `country.flag` in each league

### Web component tests
- `LeaguesList` displays flag image and country name per row
- `LeagueDetail` displays country name and flag in the detail panel
