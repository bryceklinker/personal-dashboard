# League Seasons & Detail Panels Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `FootballLeagueSeason` entity, require explicit season params in `RefreshClubsCommand`, sort lists by favorite-first, and add detail panels to both the Leagues and Clubs pages.

**Architecture:** New `FootballLeagueSeason` entity (composite PK `LeagueId+Year`) replaces the `CurrentSeasonYear` scalar on `FootballLeagueEntity`. All three command handlers that trigger club loading (RefreshLeagues, FavoriteLeague, RefreshAllFavoritedClubs) load the current season from the DB and pass it explicitly to `RefreshClubsCommand(Guid LeagueId, int SeasonYear)`. Detail panels are Blazor components wired via `EventCallback<T>` selection callbacks.

**Tech Stack:** .NET 10, EF Core, MediatR CQRS, Blazor WASM, MudBlazor, AutoMapper, Football API (api-sports.com), xUnit, bUnit, Playwright

---

## File Structure

**Create:**
- `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueSeason.cs` — new entity + EF config (add into existing file alongside `FootballLeagueEntityConfiguration`)
- `src/Personal.Dashboard.Core/Clubs/Commands/RefreshAllFavoritedClubsCommand.cs` — new command + handler
- (none — existing `FootballClubMapper.cs` is modified in-place)
- `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor` — new detail panel component
- `src/Personal.Dashboard.Web.Host/Clubs/ClubDetail.razor` — new detail panel component
- `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs` — component tests
- `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubDetailTests.cs` — component tests

**Modify:**
- `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs` — remove `CurrentSeasonYear`, add `Seasons` navigation, update `UpdateFromFootballApi`
- `src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs` — new required `(Guid LeagueId, int SeasonYear)` signature; remove `HandleAllLeagues`
- `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs` — upsert seasons per league; dispatch per-league `RefreshClubsCommand`
- `src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs` — include seasons; dispatch with current season year
- `src/Personal.Dashboard.Core/Leagues/Queries/GetLeaguesQuery.cs` — include seasons; sort by `IsFavorite` desc, `Name` asc
- `src/Personal.Dashboard.Core/Clubs/Queries/GetClubsQuery.cs` — include leagues; sort by `IsFavorite` desc, `Name` asc
- `src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs` — add `FootballLeagueSeason → FootballLeagueSeasonModel` mapping; update league mapping
- `src/Personal.Dashboard.Models/FootballModels.cs` — add `FootballLeagueSeasonModel`, `FootballClubLeagueModel`; update `FootballLeagueModel` and `FootballClubModel`
- `src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs` — dispatch `RefreshAllFavoritedClubsCommand` from `RefreshClubs`
- `src/Personal.Dashboard.Web.Host/Leagues/Leagues.razor` — add `_selected` state; wire `LeaguesList` + `LeagueDetail`
- `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor` — add `OnLeagueSelected` callback; show current season year; clickable rows
- `src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor` — add `OnClubSelected` callback; clickable rows
- `src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor` — add `_selected` state; wire `ClubsList` + `ClubDetail`
- `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs` — add `Season(bool current)` overload
- `tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs` — update `SetupGetTeams` to accept `int seasonYear`
- `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs` — delete stale null-LeagueId test; add season upsert tests
- `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs` — update dispatch assertion; add no-current-season test
- `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs` — delete `HandleAllLeagues` tests; update season parameter usage
- `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesControllerTests.cs` — add `Seasons[]` and sort-order tests
- `tests/Personal.Dashboard.Api.Host.Tests/Clubs/ClubsControllerTests.cs` — add `Leagues[]` and sort-order tests
- `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs` — add `SetupLeagueDetail` helper (unfavorite endpoint)
- `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs` — add click-row and season-year tests
- `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs` — add click-row test
- `tests/Personal.Dashboard.Test.Support/DataFactory.cs` — update `FootballLeagueModel()` and `FootballClubModel()` factories
- `tests/Personal.Dashboard.Feature.Tests/Leagues/LeagueFavoritingTests.cs` — update to use new season API
- `tests/Personal.Dashboard.Feature.Tests/Clubs/ClubFavoritingTests.cs` — no change needed

---

## Chunk 1: Test Support + Data Model + Migration

### Task 1: Update test support factories and HTTP handler

**Files:**
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs`

- [ ] **Step 1: Add `Season(bool current = true)` overload to `FootballApiDataFactory`**

In `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs`, replace the existing `Season()` method with an overload:

```csharp
public static FootballApiSeason Season(bool current = true)
{
    var start = Faker.Date.PastDateOnly();
    return new FootballApiSeason(
        Faker.Date.RecentDateOnly().Year,
        start,
        start.AddMonths(9),
        current,
        SeasonCoverage()
    );
}
```

The `League()` factory already calls `[Season()]` — this still works because `current` defaults to `true`.

- [ ] **Step 2: Update `SetupGetTeams` to accept explicit `int seasonYear`**

In `tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs`, update the method:

```csharp
public static async Task SetupGetTeams(
    this FakeHttpMessageHandler handler,
    string baseUrl,
    long leagueId,
    int seasonYear,
    FootballApiTeam[] teams)
{
    await handler.SetupGetJsonResponseAsync(
        $"{baseUrl}/teams?league={leagueId}&season={seasonYear}",
        FootballApiDataFactory.SuccessResponse(teams)
    );
}
```

- [ ] **Step 3: Build and verify the Core.Tests project compiles**

Run: `dotnet build tests/Personal.Dashboard.Core.Tests --configuration Release`

Expected: Build fails on callers of `SetupGetTeams` that don't pass `seasonYear` — that's expected. We will fix them in Task 8 (FavoriteLeagueCommandTests) and Task 5 (RefreshClubsCommandTests).

- [ ] **Step 4: Commit**

```bash
git add tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs \
        tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs
git commit -m "test: add Season(bool) overload and explicit seasonYear to SetupGetTeams"
```

---

### Task 2: `FootballLeagueSeason` entity + `FootballLeagueEntity` update

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`

- [ ] **Step 1: Write the failing entity test**

In `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs`, add tests (existing file — add to it):

```csharp
[Fact]
public void WhenUpdateFromFootballApiCalledThenDoesNotSetCurrentSeasonYear()
{
    var entity = PersonalDashboardEntityFactory.FootballLeague();
    var apiLeague = FootballApiDataFactory.League();

    entity.UpdateFromFootballApi(apiLeague);

    // CurrentSeasonYear no longer exists — this test verifies Seasons is empty until upserted
    Assert.Empty(entity.Seasons);
}
```

- [ ] **Step 2: Run the test to verify it fails** (it won't compile yet)

Run: `dotnet build tests/Personal.Dashboard.Core.Tests --configuration Release`
Expected: compile error — `entity.Seasons` doesn't exist yet.

- [ ] **Step 3: Add `FootballLeagueSeason` entity + update `FootballLeagueEntity`**

Replace the entire content of `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueSeason
{
    public Guid LeagueId { get; set; }
    public int Year { get; set; }
    public bool IsCurrent { get; set; }
    public required FootballLeagueEntity League { get; set; }
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

public class FootballLeagueEntity
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = "";
    public DateTimeOffset? LastRefreshed { get; set; }
    public bool IsFavorite { get; set; }

    public ICollection<FootballLeagueAlias> Aliases { get; set; } = new List<FootballLeagueAlias>();
    public ICollection<FootballClubEntity> Clubs { get; set; } = new List<FootballClubEntity>();
    public ICollection<FootballLeagueSeason> Seasons { get; set; } = new List<FootballLeagueSeason>();

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballLeagueAlias
        {
            AliasSource = source,
            Alias = alias,
            League = this,
        });
    }

    public void Favorite() => IsFavorite = true;
    public void Unfavorite() => IsFavorite = false;

    public void UpdateFromFootballApi(FootballApiLeague league)
    {
        Name = league.League.Name;
        LastRefreshed = DateTimeOffset.UtcNow;
    }
}

public class FootballLeagueEntityConfiguration : IEntityTypeConfiguration<FootballLeagueEntity>
{
    public void Configure(EntityTypeBuilder<FootballLeagueEntity> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(p => p.Name).IsRequired();

        builder.HasMany(p => p.Aliases)
            .WithOne(a => a.League)
            .HasForeignKey(a => a.LeagueId);
    }
}
```

- [ ] **Step 4: Run the test**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "WhenUpdateFromFootballApiCalledThenDoesNotSetCurrentSeasonYear"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs
git commit -m "feat: add FootballLeagueSeason entity; remove CurrentSeasonYear from FootballLeagueEntity"
```

---

### Task 3: EF Core migration

**Files:**
- Create: `src/Personal.Dashboard.Migrations.Host/Migrations/<timestamp>_AddFootballLeagueSeasons.cs` (auto-generated)

- [ ] **Step 1: Generate the migration**

Run from the repo root:
```bash
dotnet ef migrations add AddFootballLeagueSeasons \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```

Expected: New migration file created in `src/Personal.Dashboard.Migrations.Host/Migrations/`.

- [ ] **Step 2: Verify the migration contents**

Open the generated migration file and confirm:
- `migrationBuilder.DropColumn("CurrentSeasonYear", "FootballLeagueEntities")` (or similar table name)
- `migrationBuilder.CreateTable("FootballLeagueSeasons", ...)` with columns `LeagueId` (FK) and `Year` (int), and primary key on both

- [ ] **Step 3: Build the migrations project**

Run: `dotnet build src/Personal.Dashboard.Migrations.Host --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Personal.Dashboard.Migrations.Host/Migrations/
git commit -m "feat: migration to add FootballLeagueSeasons table and drop CurrentSeasonYear"
```

---

### Task 4: Shared models + DataFactory updates

**Files:**
- Modify: `src/Personal.Dashboard.Models/FootballModels.cs`
- Modify: `tests/Personal.Dashboard.Test.Support/DataFactory.cs`

- [ ] **Step 1: Update `FootballModels.cs`**

Replace the entire file:

```csharp
namespace Personal.Dashboard.Models;

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

- [ ] **Step 2: Update `DataFactory` factories to build models with Seasons/Leagues**

In `tests/Personal.Dashboard.Test.Support/DataFactory.cs`, update both model factories:

```csharp
public static FootballLeagueModel FootballLeagueModel()
{
    return new FootballLeagueModel(
        Faker.Random.Guid(),
        Faker.Company.CompanyName(),
        Faker.Date.RecentOffset(),
        Faker.Random.Bool(),
        [new FootballLeagueSeasonModel(Faker.Random.Int(2020, 2025), true)]
    );
}

public static FootballClubModel FootballClubModel()
{
    return new FootballClubModel(
        Faker.Random.Guid(),
        Faker.Company.CompanyName(),
        Faker.Date.RecentOffset(),
        Faker.Random.Bool(),
        [new FootballClubLeagueModel(Faker.Random.Guid(), Faker.Company.CompanyName())]
    );
}
```

- [ ] **Step 3: Build the Models and Test.Support projects**

Run: `dotnet build src/Personal.Dashboard.Models src/Personal.Dashboard.Web.Host --configuration Release`
Expected: Models builds; Web.Host may fail on razor components that use `FootballLeagueModel` without `Seasons` — that's expected and will be fixed in the UI tasks.

Note: The `IsFavorite = false` default on both records is removed — it is now a required positional parameter. The build will surface all call sites that omitted it; fix them by supplying an explicit value.

- [ ] **Step 4: Commit**

```bash
git add src/Personal.Dashboard.Models/FootballModels.cs \
        tests/Personal.Dashboard.Test.Support/DataFactory.cs
git commit -m "feat: add FootballLeagueSeasonModel, FootballClubLeagueModel; update shared models"
```

---

### Task 5: AutoMapper profiles

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs`
- Modify: `src/Personal.Dashboard.Core/Clubs/Mappers/FootballClubMapper.cs` (**modify in-place** — do NOT create a new file alongside it; `ApplyConfigurationsFromAssembly` / `AddAutoMapper(Assembly)` would register both and throw `AutoMapperConfigurationException` for duplicate `FootballClubEntity → FootballClubModel` mappings)

- [ ] **Step 1: Update `LeaguesMappingProfile`**

Replace entire file:

```csharp
using AutoMapper;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Leagues.Mappers;

public class LeaguesMappingProfile : Profile
{
    public LeaguesMappingProfile()
    {
        CreateMap<FootballLeagueSeason, FootballLeagueSeasonModel>();
        CreateMap<FootballLeagueEntity, FootballLeagueModel>()
            .ForMember(d => d.Seasons, o => o.MapFrom(s => s.Seasons));
    }
}
```

- [ ] **Step 2: Update `FootballClubMapper.cs` to add league mappings**

Replace entire file `src/Personal.Dashboard.Core/Clubs/Mappers/FootballClubMapper.cs`:

```csharp
using AutoMapper;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Mappers;

public class FootballClubMapper : Profile
{
    public FootballClubMapper()
    {
        CreateMap<FootballLeagueEntity, FootballClubLeagueModel>();
        CreateMap<FootballClubEntity, FootballClubModel>()
            .ForMember(d => d.Leagues, o => o.MapFrom(s => s.Leagues));
    }
}
```

- [ ] **Step 3: Build the Core project**

Run: `dotnet build src/Personal.Dashboard.Core --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs \
        src/Personal.Dashboard.Core/Clubs/Mappers/FootballClubMapper.cs
git commit -m "feat: add season and league mappings to AutoMapper profiles"
```

---

## Chunk 2: Backend Command & Query Changes

### Task 6: `RefreshClubsCommand` — new signature, remove `HandleAllLeagues`

**Files:**
- Modify: `src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs`

- [ ] **Step 1: Delete `HandleAllLeagues` tests and rewrite the test file**

Replace `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class RefreshClubsCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private const int SeasonYear = 2025;
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshClubsCommandTests()
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
    public async Task WhenLeagueExistsThenRefreshesClubsForThatLeague()
    {
        var leagueApiAlias = "100";
        var clubApiAlias = "200";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id, SeasonYear));

        var dbClub = await _context.Set<FootballClubEntity>().FirstOrDefaultAsync(c => c.Id == club.Id);
        Assert.NotNull(dbClub?.LastRefreshed);
        Assert.Equal(apiTeamWithKnownId.Team.Name, dbClub?.Name);
    }

    [Fact]
    public async Task WhenNewClubReturnedFromApiThenCreatesClub()
    {
        var leagueApiAlias = "101";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, [apiTeam]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id, SeasonYear));

        var dbClubs = await _context.Set<FootballClubEntity>().ToArrayAsync();
        Assert.Single(dbClubs);
        Assert.Equal(apiTeam.Team.Name, dbClubs[0].Name);
        Assert.NotNull(dbClubs[0].LastRefreshed);
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAnyAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new RefreshClubsCommand(Guid.NewGuid(), SeasonYear)));
    }

    [Fact]
    public async Task WhenRefreshCompletedThenPublishesClubsRefreshedEvent()
    {
        var leagueApiAlias = "102";
        var clubApiAlias = "202";
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        _context.Add(club);
        await _context.SaveChangesAsync();

        var apiTeam = FootballApiDataFactory.Team();
        var apiTeamWithKnownId = apiTeam with { Team = apiTeam.Team with { Id = long.Parse(clubApiAlias) } };
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, [apiTeamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id, SeasonYear));

        Assert.Single(_cqrsBus.GetCapturedEvents<ClubsRefreshedEvent>());
    }

    [Fact]
    public async Task WhenApiCalledThenUsesExplicitSeasonYear()
    {
        var leagueApiAlias = "103";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();

        // SeasonYear 2024 — distinct from calendar year 2026 to confirm explicit param is used
        const int specificSeason = 2024;
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), specificSeason, []);

        // Should succeed because the handler uses SeasonYear=2024 when calling the API
        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id, specificSeason));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshClubsCommandTests"`
Expected: Build errors because `RefreshClubsCommand` still takes `Guid?` and the callers now pass two params.

- [ ] **Step 3: Update `RefreshClubsCommand` implementation**

Replace the entire `src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record RefreshClubsCommand(Guid LeagueId, int SeasonYear) : ICommand;

public class RefreshClubsCommandHandler(
    PersonalDashboardContext db,
    IFootballApiClient footballApiClient,
    ICqrsBus bus
) : ICommandHandler<RefreshClubsCommand>
{
    public async Task Handle(RefreshClubsCommand request, CancellationToken cancellationToken)
    {
        var league = await db.Set<FootballLeagueEntity>()
            .Include(l => l.Aliases)
            .FirstOrDefaultAsync(l => l.Id == request.LeagueId, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);

        var faAlias = league.Aliases.FirstOrDefault(a => a.AliasSource == DataSource.FootballApi)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueAlias), request.LeagueId);

        var existingAliases = await db.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Club)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        var response = await footballApiClient.GetTeamsAsync(
            new FootballApiTeamsParameters(League: long.Parse(faAlias.Alias), Season: request.SeasonYear));

        ProcessTeams(response.Response, league, existingAliases, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await bus.PublishAsync(new ClubsRefreshedEvent(), cancellationToken);
    }

    private void ProcessTeams(
        FootballApiTeam[] teams,
        FootballLeagueEntity league,
        Dictionary<string, FootballClubAlias> existingAliases,
        CancellationToken cancellationToken)
    {
        foreach (var team in teams)
        {
            var aliasKey = team.Team.Id.ToString();
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.Club.UpdateFromFootballApi(team);
            }
            else
            {
                var club = new FootballClubEntity();
                club.AddAlias(DataSource.FootballApi, aliasKey);
                club.AddLeague(league);
                club.UpdateFromFootballApi(team);
                db.Set<FootballClubEntity>().Add(club);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshClubsCommandTests"`
Expected: All 5 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs
git commit -m "feat: require explicit LeagueId+SeasonYear in RefreshClubsCommand; remove HandleAllLeagues"
```

---

### Task 7: `RefreshAllFavoritedClubsCommand` + `ClubsController` update

**Files:**
- Create: `src/Personal.Dashboard.Core/Clubs/Commands/RefreshAllFavoritedClubsCommand.cs`
- Modify: `src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs`

- [ ] **Step 1: Write the test**

In `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/` create `RefreshAllFavoritedClubsCommandTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class RefreshAllFavoritedClubsCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private const int SeasonYear = 2025;
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public RefreshAllFavoritedClubsCommandTests()
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
    public async Task WhenFavoritedLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommand()
    {
        const string leagueApiAlias = "500";
        var league = PersonalDashboardEntityFactory.FootballLeague(l =>
        {
            l.Favorite();
            l.AddAlias(DataSource.FootballApi, leagueApiAlias);
        });
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, []);

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == league.Id && c.SeasonYear == SeasonYear);
    }

    [Fact]
    public async Task WhenFavoritedLeagueHasNoCurrentSeasonThenSkipsIt()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }

    [Fact]
    public async Task WhenLeagueIsNotFavoritedThenSkipsIt()
    {
        const string leagueApiAlias = "501";
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new RefreshAllFavoritedClubsCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshAllFavoritedClubsCommandTests"`
Expected: Build error — `RefreshAllFavoritedClubsCommand` doesn't exist yet.

- [ ] **Step 3: Create `RefreshAllFavoritedClubsCommand`**

Create `src/Personal.Dashboard.Core/Clubs/Commands/RefreshAllFavoritedClubsCommand.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record RefreshAllFavoritedClubsCommand : ICommand;

public class RefreshAllFavoritedClubsCommandHandler(
    PersonalDashboardContext db,
    ICqrsBus bus
) : ICommandHandler<RefreshAllFavoritedClubsCommand>
{
    public async Task Handle(RefreshAllFavoritedClubsCommand request, CancellationToken cancellationToken)
    {
        var leagues = await db.Set<FootballLeagueEntity>()
            .Where(l => l.IsFavorite && l.Seasons.Any(s => s.IsCurrent))
            .Include(l => l.Seasons)
            .ToListAsync(cancellationToken);

        foreach (var league in leagues)
        {
            var current = league.Seasons.First(s => s.IsCurrent);
            await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, current.Year), cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Update `ClubsController` to use the new command**

Replace `RefreshClubs` in `src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs`:

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshClubs()
{
    return await ExecuteAsync(new RefreshAllFavoritedClubsCommand(), statusCode: 204);
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshAllFavoritedClubsCommandTests"`
Expected: All 3 pass.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/Commands/RefreshAllFavoritedClubsCommand.cs \
        src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs \
        tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshAllFavoritedClubsCommandTests.cs
git commit -m "feat: add RefreshAllFavoritedClubsCommand; wire into ClubsController"
```

---

### Task 8: `RefreshLeaguesCommandHandler` — season upsert + per-league dispatch

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs`

- [ ] **Step 1: Rewrite `RefreshLeaguesCommandTests`**

Replace the file entirely:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Leagues.Events;
using Personal.Dashboard.Core.Tests.Support;

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

    [Fact]
    public async Task WhenRefreshingLeaguesThenUpsertsSeasonsForEachLeague()
    {
        var apiSeason = FootballApiDataFactory.Season(current: true);
        var apiLeague = FootballApiDataFactory.League() with { Seasons = [apiSeason] };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var seasons = await _context.Set<FootballLeagueSeason>().ToArrayAsync();
        Assert.Single(seasons);
        Assert.Equal((int)apiSeason.Year, seasons[0].Year);
        Assert.True(seasons[0].IsCurrent);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenUpdatesExistingSeasonIsCurrent()
    {
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague();
        existingLeague.AddAlias(DataSource.FootballApi, "43");
        existingLeague.Seasons.Add(new FootballLeagueSeason { Year = 2024, IsCurrent = true, League = existingLeague });
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        // API now says 2025 is current and 2024 is not
        var apiLeague = FootballApiDataFactory.League() with
        {
            League = FootballApiDataFactory.LeagueInfo() with { Id = 43 },
            Seasons =
            [
                FootballApiDataFactory.Season(current: false) with { Year = 2024 },
                FootballApiDataFactory.Season(current: true) with { Year = 2025 }
            ]
        };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var seasons = await _context.Set<FootballLeagueSeason>()
            .OrderBy(s => s.Year)
            .ToArrayAsync();
        Assert.Equal(2, seasons.Length);
        Assert.False(seasons[0].IsCurrent); // 2024
        Assert.True(seasons[1].IsCurrent);  // 2025
    }

    [Fact]
    public async Task WhenFavoritedLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommand()
    {
        const string leagueApiAlias = "44";
        var existingLeague = PersonalDashboardEntityFactory.FootballLeague(l =>
        {
            l.Favorite();
            l.AddAlias(DataSource.FootballApi, leagueApiAlias);
        });
        _context.Add(existingLeague);
        await _context.SaveChangesAsync();

        var apiSeason = FootballApiDataFactory.Season(current: true) with { Year = 2025 };
        var apiLeague = FootballApiDataFactory.League() with
        {
            League = FootballApiDataFactory.LeagueInfo() with { Id = 44 },
            Seasons = [apiSeason]
        };
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);
        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), 2025, []);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == existingLeague.Id && c.SeasonYear == 2025);
    }

    [Fact]
    public async Task WhenLeagueIsNotFavoritedThenDoesNotDispatchRefreshClubsCommand()
    {
        var apiLeague = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

        await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }
}
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshLeaguesCommandTests"`
Expected: Season-related tests fail; stale `LeagueId == null` test is gone.

- [ ] **Step 3: Update `RefreshLeaguesCommandHandler`**

Replace `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Commands;
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
            .ThenInclude(l => l.Seasons)
            .ToDictionaryAsync(a => a.Alias, cancellationToken);

        foreach (var apiLeague in response.Response)
        {
            var aliasKey = $"{apiLeague.League.Id}";
            FootballLeagueEntity entity;
            if (existingAliases.TryGetValue(aliasKey, out var alias))
            {
                alias.League.UpdateFromFootballApi(apiLeague);
                entity = alias.League;
            }
            else
            {
                entity = new FootballLeagueEntity();
                entity.AddAlias(DataSource.FootballApi, aliasKey);
                entity.UpdateFromFootballApi(apiLeague);
                context.Add(entity);
            }

            UpsertSeasons(entity, apiLeague.Seasons);
        }

        await context.SaveChangesAsync(cancellationToken);

        var favoritedWithCurrentSeason = await context.Set<FootballLeagueEntity>()
            .Where(l => l.IsFavorite && l.Seasons.Any(s => s.IsCurrent))
            .Include(l => l.Seasons)
            .ToListAsync(cancellationToken);

        foreach (var league in favoritedWithCurrentSeason)
        {
            var currentSeason = league.Seasons.First(s => s.IsCurrent);
            await bus.ExecuteAsync(new RefreshClubsCommand(league.Id, currentSeason.Year), cancellationToken);
        }

        await bus.PublishAsync(new LeaguesRefreshedEvent(), cancellationToken);
    }

    private static void UpsertSeasons(FootballLeagueEntity entity, FootballApiSeason[] apiSeasons)
    {
        var existingSeasons = entity.Seasons.ToDictionary(s => s.Year);
        foreach (var apiSeason in apiSeasons)
        {
            var year = (int)apiSeason.Year;
            if (existingSeasons.TryGetValue(year, out var season))
                season.IsCurrent = apiSeason.Current;
            else
                // `required FootballLeagueEntity League` must be set explicitly; EF Core handles the FK via the navigation
                entity.Seasons.Add(new FootballLeagueSeason { Year = year, IsCurrent = apiSeason.Current, League = entity });
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshLeaguesCommandTests"`
Expected: All 7 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs
git commit -m "feat: upsert seasons in RefreshLeaguesCommand; dispatch per-league RefreshClubsCommand"
```

---

### Task 9: `FavoriteLeagueCommandHandler` — include seasons, dispatch with season year

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs`

- [ ] **Step 1: Rewrite `FavoriteLeagueCommandTests`**

Replace the file:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class FavoriteLeagueCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private const int SeasonYear = 2025;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;
    private readonly FakeHttpMessageHandler _handler;

    public FavoriteLeagueCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(
            configure: opts => opts.ConfigureFootballApi = api => api.BaseUrl = BaseUrl,
            configureServices: services => services.AddCapturingCqrsBus()
        );
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _cqrsBus = provider.GetRequiredService<CapturingCqrsBus>();
        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
    }

    [Fact]
    public async Task WhenLeagueExistsThenSetsIsFavoriteToTrue()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        _context.Add(league);
        await _context.SaveChangesAsync();

        // No current season → no RefreshClubsCommand → no HTTP setup needed
        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenLeagueHasCurrentSeasonThenDispatchesRefreshClubsCommandWithSeasonYear()
    {
        const string leagueApiAlias = "301";
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        league.Seasons.Add(new FootballLeagueSeason { Year = SeasonYear, IsCurrent = true, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _handler.SetupGetTeams(BaseUrl, long.Parse(leagueApiAlias), SeasonYear, []);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>();
        Assert.Single(dispatched, c => c.LeagueId == league.Id && c.SeasonYear == SeasonYear);
    }

    [Fact]
    public async Task WhenLeagueHasNoCurrentSeasonThenDoesNotDispatchRefreshClubsCommand()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Seasons.Add(new FootballLeagueSeason { Year = 2024, IsCurrent = false, League = league });
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        Assert.Empty(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
    }

    [Fact]
    public async Task WhenLeagueDoesNotExistThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(Guid.NewGuid())));
    }
}
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteLeagueCommandTests"`
Expected: Failures — dispatch assertion checks `SeasonYear`; handler doesn't yet include seasons.

- [ ] **Step 3: Update `FavoriteLeagueCommandHandler`**

Replace `src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record FavoriteLeagueCommand(Guid LeagueId) : ICommand;

public class FavoriteLeagueCommandHandler(
    PersonalDashboardContext db,
    ICqrsBus bus,
    ILogger<FavoriteLeagueCommandHandler> logger
) : ICommandHandler<FavoriteLeagueCommand>
{
    public async Task Handle(FavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
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
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteLeagueCommandTests"`
Expected: All 4 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs
git commit -m "feat: FavoriteLeagueCommand uses current season year when dispatching RefreshClubsCommand"
```

---

### Task 10: `GetLeaguesQuery` + `GetClubsQuery` sorting and include

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Queries/GetLeaguesQuery.cs`
- Modify: `src/Personal.Dashboard.Core/Clubs/Queries/GetClubsQuery.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Queries/GetLeaguesQueryTests.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Clubs/Queries/GetClubsQueryTests.cs`

- [ ] **Step 1: Add sorting tests to `GetLeaguesQueryTests`**

Add these tests to the existing file:

```csharp
[Fact]
public async Task WhenMixedFavoritesThenReturnsFavoritesFirst()
{
    var favoritedLeague = PersonalDashboardEntityFactory.FootballLeague(l => { l.Favorite(); });
    var unfavoritedLeague = PersonalDashboardEntityFactory.FootballLeague();
    _context.Add(unfavoritedLeague);
    _context.Add(favoritedLeague);
    await _context.SaveChangesAsync();

    var result = await _bus.QueryAsync(new GetLeaguesQuery(Offset: 0, Limit: 100));

    Assert.True(result.Items[0].IsFavorite);
    Assert.False(result.Items[1].IsFavorite);
}

[Fact]
public async Task WhenLeagueHasSeasonsThenReturnsSeasonsInModel()
{
    var league = PersonalDashboardEntityFactory.FootballLeague();
    league.Seasons.Add(new FootballLeagueSeason { Year = 2025, IsCurrent = true, League = league });
    _context.Add(league);
    await _context.SaveChangesAsync();

    var result = await _bus.QueryAsync(new GetLeaguesQuery());

    Assert.Single(result.Items[0].Seasons, s => s.Year == 2025 && s.IsCurrent);
}
```

Note: `FootballLeagueSeason` is in `Personal.Dashboard.Core.Leagues.Entities` — add `using Personal.Dashboard.Core.Leagues.Entities;` to the file.

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "GetLeaguesQueryTests"`
Expected: New tests fail because `GetLeaguesQuery` doesn't include Seasons or sort by IsFavorite.

- [ ] **Step 3: Update `GetLeaguesQuery`**

Replace file:

```csharp
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Leagues.Queries;

public record GetLeaguesQuery(int Offset = 0, int Limit = 10) : PagedQuery<FootballLeagueModel>(Offset, Limit);

public class GetLeaguesQueryHandler(
    PersonalDashboardContext context,
    IMapper mapper
) : IQueryHandler<GetLeaguesQuery, PagedListResultModel<FootballLeagueModel>>
{
    public async Task<PagedListResultModel<FootballLeagueModel>> Handle(GetLeaguesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Set<FootballLeagueEntity>()
            .Include(l => l.Seasons)
            .OrderByDescending(l => l.IsFavorite)
            .ThenBy(l => l.Name)
            .ProjectTo<FootballLeagueModel>(mapper.ConfigurationProvider);

        return await query.ToPagedListAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Add sorting tests to `GetClubsQueryTests`**

In `tests/Personal.Dashboard.Core.Tests/Clubs/Queries/GetClubsQueryTests.cs`, add:

```csharp
[Fact]
public async Task WhenMixedFavoritesThenReturnsFavoritesFirst()
{
    var favorited = PersonalDashboardEntityFactory.FootballClub(c => c.Favorite());
    var unfavorited = PersonalDashboardEntityFactory.FootballClub();
    _context.Add(unfavorited);
    _context.Add(favorited);
    await _context.SaveChangesAsync();

    var result = await _bus.QueryAsync(new GetClubsQuery(Offset: 0, Limit: 100));

    Assert.True(result.Items[0].IsFavorite);
    Assert.False(result.Items[1].IsFavorite);
}

[Fact]
public async Task WhenClubBelongsToLeagueThenReturnsLeaguesInModel()
{
    var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague();
    _context.Add(league);
    _context.Add(club);
    await _context.SaveChangesAsync();

    var result = await _bus.QueryAsync(new GetClubsQuery());

    Assert.Single(result.Items[0].Leagues, l => l.Id == league.Id);
}
```

- [ ] **Step 5: Update `GetClubsQuery`**

Replace file:

```csharp
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Queries;

public record GetClubsQuery(int Offset = 0, int Limit = 10) : PagedQuery<FootballClubModel>(Offset, Limit);

public class GetClubsQueryHandler(
    PersonalDashboardContext context,
    IMapper mapper
) : IQueryHandler<GetClubsQuery, PagedListResultModel<FootballClubModel>>
{
    public async Task<PagedListResultModel<FootballClubModel>> Handle(GetClubsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Set<FootballClubEntity>()
            .Include(c => c.Leagues)
            .OrderByDescending(c => c.IsFavorite)
            .ThenBy(c => c.Name)
            .ProjectTo<FootballClubModel>(mapper.ConfigurationProvider);
        return await query.ToPagedListAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Run all Core.Tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Queries/GetLeaguesQuery.cs \
        src/Personal.Dashboard.Core/Clubs/Queries/GetClubsQuery.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Queries/GetLeaguesQueryTests.cs \
        tests/Personal.Dashboard.Core.Tests/Clubs/Queries/GetClubsQueryTests.cs
git commit -m "feat: sort leagues and clubs by IsFavorite desc then Name asc; include seasons and leagues"
```

---

### Task 11: API integration tests

**Files:**
- Modify: `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesControllerTests.cs`
- Modify: `tests/Personal.Dashboard.Api.Host.Tests/Clubs/ClubsControllerTests.cs`
- Modify: `tests/Personal.Dashboard.Api.Host.Tests/Support/PersonalDashboardApiApplication.cs` (verify `FootballLeagueSeason` support)

First check if `PersonalDashboardApiApplication` needs updates to support `FootballLeagueSeason` in `AddToDbAsync`. It uses EF Core directly so it should work automatically.

- [ ] **Step 1: Update `LeaguesControllerTests`**

Add these tests to the existing file:

```csharp
[Fact]
public async Task WhenGetLeaguesCalledThenReturnsSeasonsInModel()
{
    const string leagueApiAlias = "778";
    var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
    await app.AddToDbAsync(league);
    await app.AddToDbAsync(new FootballLeagueSeason { LeagueId = league.Id, Year = 2025, IsCurrent = true, League = league });

    var response = await _client.GetAsync("/leagues");
    var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();

    var leagueModel = result?.Items.FirstOrDefault(l => l.Id == league.Id);
    Assert.NotNull(leagueModel);
    Assert.Single(leagueModel.Seasons, s => s.Year == 2025 && s.IsCurrent);
}

[Fact]
public async Task WhenGetLeaguesCalledThenReturnsFavoritesFirst()
{
    var favorite = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
    var unfavorite = PersonalDashboardEntityFactory.FootballLeague();
    await app.AddToDbAsync(favorite);
    await app.AddToDbAsync(unfavorite);

    var response = await _client.GetAsync("/leagues?limit=100");
    var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();

    Assert.NotNull(result);
    var favoriteIndex = Array.FindIndex(result.Items, l => l.Id == favorite.Id);
    var unfavoriteIndex = Array.FindIndex(result.Items, l => l.Id == unfavorite.Id);
    Assert.True(favoriteIndex < unfavoriteIndex);
}
```

Add required usings: `using Personal.Dashboard.Core.Leagues.Entities;` and `using System.Net.Http.Json;`.

- [ ] **Step 2: Update `ClubsControllerTests`**

Add these tests to the existing file:

```csharp
[Fact]
public async Task WhenGetClubsCalledThenReturnsLeaguesInModel()
{
    var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague("999", "888");
    await app.AddToDbAsync(league);
    await app.AddToDbAsync(club);

    var response = await _client.GetAsync("/clubs");
    var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballClubModel>>();

    var clubModel = result?.Items.FirstOrDefault(c => c.Id == club.Id);
    Assert.NotNull(clubModel);
    Assert.Single(clubModel.Leagues, l => l.Id == league.Id);
}

[Fact]
public async Task WhenGetClubsCalledThenReturnsFavoritesFirst()
{
    var favorite = PersonalDashboardEntityFactory.FootballClub(c => c.Favorite());
    var unfavorite = PersonalDashboardEntityFactory.FootballClub();
    await app.AddToDbAsync(favorite);
    await app.AddToDbAsync(unfavorite);

    var response = await _client.GetAsync("/clubs?limit=100");
    var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballClubModel>>();

    Assert.NotNull(result);
    var favoriteIndex = Array.FindIndex(result.Items, c => c.Id == favorite.Id);
    var unfavoriteIndex = Array.FindIndex(result.Items, c => c.Id == unfavorite.Id);
    Assert.True(favoriteIndex < unfavoriteIndex);
}
```

- [ ] **Step 3: Update `LeaguesControllerTests.WhenFavoriteLeagueCalledThenReturns204`**

The existing test calls `SetupGetTeams` without a season year. Now `FavoriteLeagueCommand` only dispatches `RefreshClubsCommand` if there's a current season in the DB. Update the test to add a current season and pass the season year to `SetupGetTeams`:

```csharp
[Fact]
public async Task WhenFavoriteLeagueCalledThenReturns204()
{
    const string leagueApiAlias = "777";
    const int seasonYear = 2025;
    var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
    await app.AddToDbAsync(league);
    await app.AddToDbAsync(new FootballLeagueSeason { LeagueId = league.Id, Year = seasonYear, IsCurrent = true, League = league });
    await app.HttpHandler.SetupGetTeams(
        PersonalDashboardApiApplication.FootballApiBaseUrl,
        long.Parse(leagueApiAlias),
        seasonYear,
        []
    );

    var response = await _client.PostAsync($"/leagues/{league.Id}/favorite", null);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
}
```

- [ ] **Step 4: Run API integration tests**

Run: `dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesControllerTests.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Clubs/ClubsControllerTests.cs
git commit -m "test: add API integration tests for seasons, leagues, and sort order"
```

---

## Chunk 3: UI Components

### Task 12: `LeaguesList` — add selection callback + season year display

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`

- [ ] **Step 1: Add tests for selection and season display**

Add to `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`:

```csharp
[Fact]
public async Task WhenLeagueRowClickedThenInvokesOnLeagueSelected()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel();
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    FootballLeagueModel? selected = null;
    var page = context.Render<LeaguesList>(parameters =>
        parameters.Add(p => p.OnLeagueSelected, EventCallback.Factory.Create<FootballLeagueModel>(
            context, m => selected = m)));

    await Eventually.Assert(() =>
        page.FindComponents<MudListItem<FootballLeagueModel>>().Count > 0);

    await page.FindComponents<MudListItem<FootballLeagueModel>>()[0].Find("div[role='button']").ClickAsync();

    await Eventually.Assert(() => Assert.Equal(league.Id, selected?.Id));
}

[Fact]
public async Task WhenLeagueHasCurrentSeasonThenDisplaysSeasonYear()
{
    await using var context = new PersonalDashboardWebContext();
    var season = new FootballLeagueSeasonModel(2025, true);
    var league = DataFactory.FootballLeagueModel() with { Seasons = [season] };
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<LeaguesList>();

    await Eventually.Assert(() => Assert.Contains("2025", page.Markup));
}
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeaguesListTests"`
Expected: New tests fail.

- [ ] **Step 3: Update `LeaguesList.razor`**

Replace `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`:

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis
@using MudBlazor

@inject PersonalDashboardApiClient Client
@inject ISnackbar Snackbar
@implements IAsyncDisposable

<MudGrid>
    <MudItem xs="12">
        <MudGrid>
            <MudItem xs="10">
                <MudText Typo="Typo.h6">Leagues</MudText>
            </MudItem>
            <MudItem xs="2">
                <MudIconButton Icon="@Icons.Material.Rounded.Refresh"
                               OnClick="@OnRefreshClicked"
                               role="button"
                               aria-label="refresh" />
            </MudItem>
        </MudGrid>
    </MudItem>
    <MudItem xs="12">
        <MudPaper>
            <MudList T="FootballLeagueModel">
                @foreach (var league in Leagues.Items)
                {
                    var currentYear = league.Seasons.FirstOrDefault(s => s.IsCurrent)?.Year.ToString() ?? "";
                    <MudListItem>
                        <div role="button" style="cursor:pointer;width:100%" @onclick="@(() => OnLeagueSelected.InvokeAsync(league))">
                            <MudGrid>
                                <MudItem xs="10">
                                    <MudText Typo="Typo.body1">@league.Name</MudText>
                                    @if (!string.IsNullOrEmpty(currentYear))
                                    {
                                        <MudText Typo="Typo.caption">@currentYear season</MudText>
                                    }
                                    @if (league.LastRefreshed.HasValue)
                                    {
                                        <MudText Typo="Typo.caption">@league.LastRefreshed.Value.ToString("g")</MudText>
                                    }
                                </MudItem>
                                <MudItem xs="2">
                                    <MudIconButton Icon="@(league.IsFavorite ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarOutline)"
                                                   OnClick="@(e => { e.StopPropagation(); _ = OnToggleFavoriteAsync(league); })"
                                                   role="button"
                                                   aria-label="@(league.IsFavorite ? "unfavorite" : "favorite")" />
                                </MudItem>
                            </MudGrid>
                        </div>
                    </MudListItem>
                }
            </MudList>
            <MudGrid Justify="Justify.Center">
                <MudItem>
                    <MudIconButton Icon="@Icons.Material.Rounded.NavigateBefore"
                                   OnClick="@GoToPrevious"
                                   role="button"
                                   aria-label="previous" />
                    <MudIconButton Icon="@Icons.Material.Rounded.NavigateNext"
                                   OnClick="@GoToNext"
                                   Disabled="@(!HasNextPage)"
                                   role="button"
                                   aria-label="next" />
                </MudItem>
            </MudGrid>
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    [Parameter] public EventCallback<FootballLeagueModel> OnLeagueSelected { get; set; }

    public PagedListResultModel<FootballLeagueModel> Leagues { get; private set; } =
        PagedListResultModel<FootballLeagueModel>.Empty();

    private bool HasNextPage => Leagues.Total > _currentParameters.Offset + Leagues.Limit;
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

    public async Task LoadLeaguesAsync()
    {
        Leagues = await Client.GetLeaguesAsync(_currentParameters);
    }

    private async Task OnRefreshClicked()
    {
        await Client.RefreshLeaguesAsync();
    }

    private async Task OnToggleFavoriteAsync(FootballLeagueModel league)
    {
        try
        {
            if (league.IsFavorite)
                await Client.UnfavoriteLeagueAsync(league.Id);
            else
                await Client.FavoriteLeagueAsync(league.Id);
            await LoadLeaguesAsync();
            StateHasChanged();
        }
        catch
        {
            Snackbar.Add("Failed to update favorite status", Severity.Error);
        }
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

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeaguesListTests"`
Expected: All tests pass (including existing ones).

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs
git commit -m "feat: LeaguesList — add OnLeagueSelected callback and current season year display"
```

---

### Task 13: `LeagueDetail` component (new)

**Files:**
- Create: `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor`
- Create: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs`

- [ ] **Step 1: Write `LeagueDetailTests`**

Create `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs`:

```csharp
using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Leagues;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Leagues;

public class LeagueDetailTests
{
    [Fact]
    public void WhenLeagueIsNullThenShowsPlaceholder()
    {
        using var context = new PersonalDashboardWebContext();

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, null));

        Assert.Contains("Select a league to view details", page.Markup);
    }

    [Fact]
    public void WhenLeagueIsSetThenShowsLeagueName()
    {
        using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel();

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains(league.Name, page.Markup);
    }

    [Fact]
    public void WhenLeagueHasCurrentSeasonThenShowsCurrentSeasonCard()
    {
        using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with
        {
            Seasons = [new FootballLeagueSeasonModel(2025, true)]
        };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("2025", page.Markup);
        Assert.Contains("2026", page.Markup); // Year+1 displayed
    }

    [Fact]
    public void WhenLeagueHasMultipleSeasonsThenShowsAllSeasons()
    {
        using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with
        {
            Seasons =
            [
                new FootballLeagueSeasonModel(2025, true),
                new FootballLeagueSeasonModel(2024, false),
                new FootballLeagueSeasonModel(2023, false)
            ]
        };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("2025", page.Markup);
        Assert.Contains("2024", page.Markup);
        Assert.Contains("2023", page.Markup);
    }

    [Fact]
    public async Task WhenFavoriteToggledThenInvokesOnFavoriteChanged()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
        await context.HttpHandler.SetupFavoriteLeague(league.Id);

        var callbackInvoked = false;
        var page = context.Render<LeagueDetail>(p =>
        {
            p.Add(x => x.League, league);
            p.Add(x => x.OnFavoriteChanged, EventCallback.Factory.Create(context, () => callbackInvoked = true));
        });

        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.True(callbackInvoked));
    }
}
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeagueDetailTests"`
Expected: Build error — `LeagueDetail` doesn't exist yet.

- [ ] **Step 3: Create `LeagueDetail.razor`**

Create `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor`:

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis
@using MudBlazor

@inject PersonalDashboardApiClient Client
@inject ISnackbar Snackbar

@if (League is null)
{
    <MudText Typo="Typo.body1" Class="ma-4">Select a league to view details</MudText>
}
else
{
    <MudGrid>
        <MudItem xs="12">
            <MudGrid>
                <MudItem xs="10">
                    <MudText Typo="Typo.h6">@League.Name</MudText>
                    @if (League.LastRefreshed.HasValue)
                    {
                        <MudText Typo="Typo.caption">Last refreshed: @League.LastRefreshed.Value.ToString("g")</MudText>
                    }
                </MudItem>
                <MudItem xs="2">
                    <MudIconButton Icon="@(League.IsFavorite ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarOutline)"
                                   OnClick="@OnToggleFavoriteAsync"
                                   role="button"
                                   aria-label="@(League.IsFavorite ? "unfavorite" : "favorite")" />
                </MudItem>
            </MudGrid>
        </MudItem>

        @{
            var currentSeason = League.Seasons.FirstOrDefault(s => s.IsCurrent);
        }
        @if (currentSeason is not null)
        {
            <MudItem xs="12">
                <MudPaper Class="pa-3">
                    <MudText Typo="Typo.overline">Current Season</MudText>
                    <MudText Typo="Typo.body1">@currentSeason.Year – @(currentSeason.Year + 1)</MudText>
                </MudPaper>
            </MudItem>
        }

        @if (League.Seasons.Length > 0)
        {
            <MudItem xs="12">
                <MudPaper Class="pa-3">
                    <MudText Typo="Typo.overline">All Seasons</MudText>
                    <MudList T="FootballLeagueSeasonModel">
                        @foreach (var season in League.Seasons.OrderByDescending(s => s.Year))
                        {
                            <MudListItem>
                                <MudText Typo="Typo.body2">
                                    @season.Year
                                    @if (season.IsCurrent)
                                    {
                                        <MudChip T="string" Size="Size.Small" Color="Color.Primary">Current</MudChip>
                                    }
                                </MudText>
                            </MudListItem>
                        }
                    </MudList>
                </MudPaper>
            </MudItem>
        }
    </MudGrid>
}

@code {
    [Parameter] public FootballLeagueModel? League { get; set; }
    [Parameter] public EventCallback OnFavoriteChanged { get; set; }

    private async Task OnToggleFavoriteAsync()
    {
        if (League is null) return;
        try
        {
            if (League.IsFavorite)
                await Client.UnfavoriteLeagueAsync(League.Id);
            else
                await Client.FavoriteLeagueAsync(League.Id);
            await OnFavoriteChanged.InvokeAsync();
        }
        catch
        {
            Snackbar.Add("Failed to update favorite status", Severity.Error);
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeagueDetailTests"`
Expected: All 5 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs
git commit -m "feat: add LeagueDetail component with season display and favorite toggle"
```

---

### Task 14: `Leagues.razor` — wire selection + detail panel

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/Leagues.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesPageTests.cs`

- [ ] **Step 1: Add page-level test for selection**

Add to `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesPageTests.cs`:

```csharp
[Fact]
public async Task WhenLeagueSelectedThenShowsDetailPanel()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel();
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<Host.Leagues.Leagues>();
    await Eventually.Assert(() =>
        page.FindComponents<MudListItem<FootballLeagueModel>>().Count > 0);

    await page.FindComponents<MudListItem<FootballLeagueModel>>()[0]
        .Find("div[role='button']").ClickAsync();

    await Eventually.Assert(() => Assert.Contains(league.Name, page.Markup));
}
```

- [ ] **Step 2: Update `Leagues.razor`**

Replace the file:

```razor
@page "/leagues"

<MudGrid Class="d-flex flex-1">
    <MudItem xs="3" Class="d-flex flex-1">
        <LeaguesList OnLeagueSelected="@(l => { _selected = l; StateHasChanged(); })" @ref="_list" />
    </MudItem>
    <MudItem xs="9">
        <LeagueDetail League="_selected" OnFavoriteChanged="@OnFavoriteChangedAsync" />
    </MudItem>
</MudGrid>

@code {
    private FootballLeagueModel? _selected;
    private LeaguesList? _list;

    private async Task OnFavoriteChangedAsync()
    {
        if (_list is not null)
            await _list.LoadLeaguesAsync();
        if (_selected is not null && _list is not null)
        {
            _selected = _list.Leagues.Items.FirstOrDefault(l => l.Id == _selected.Id);
        }
        StateHasChanged();
    }
}
```

- [ ] **Step 3: Run page tests**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeaguesPageTests"`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Leagues/Leagues.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesPageTests.cs
git commit -m "feat: wire LeagueDetail into Leagues page with selection + favorite reload"
```

---

### Task 15: `ClubsList` — add selection callback

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs`

- [ ] **Step 1: Add row-click test**

Add to `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs`:

```csharp
[Fact]
public async Task WhenClubRowClickedThenInvokesOnClubSelected()
{
    await using var context = new PersonalDashboardWebContext();
    var club = DataFactory.FootballClubModel();
    await context.HttpHandler.SetupClubs(clubs: [club]);

    FootballClubModel? selected = null;
    var page = context.Render<ClubsList>(parameters =>
        parameters.Add(p => p.OnClubSelected, EventCallback.Factory.Create<FootballClubModel>(
            context, m => selected = m)));

    await Eventually.Assert(() =>
        page.FindComponents<MudListItem<FootballClubModel>>().Count > 0);

    await page.FindComponents<MudListItem<FootballClubModel>>()[0].Find("div[role='button']").ClickAsync();

    await Eventually.Assert(() => Assert.Equal(club.Id, selected?.Id));
}
```

- [ ] **Step 2: Update `ClubsList.razor`**

Replace `src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor`:

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis
@using MudBlazor

@inject PersonalDashboardApiClient Client
@inject ISnackbar Snackbar
@implements IAsyncDisposable

<MudGrid>
    <MudItem xs="12">
        <MudGrid>
            <MudItem xs="10">
                <MudText Typo="Typo.h6">Clubs</MudText>
            </MudItem>
            <MudItem xs="2">
                <MudIconButton Icon="@Icons.Material.Rounded.Refresh"
                               OnClick="@OnRefreshClicked"
                               role="button"
                               aria-label="refresh" />
            </MudItem>
        </MudGrid>
    </MudItem>
    <MudItem xs="12">
        <MudPaper>
            <MudList T="FootballClubModel">
                @if (!Clubs.Items.Any())
                {
                    <MudListItem>
                        <MudText Typo="Typo.body1">Clubs appear after favoriting a league</MudText>
                    </MudListItem>
                }
                @foreach (var club in Clubs.Items)
                {
                    <MudListItem>
                        <div role="button" style="cursor:pointer;width:100%" @onclick="@(() => OnClubSelected.InvokeAsync(club))">
                            <MudGrid>
                                <MudItem xs="10">
                                    <MudText Typo="Typo.body1">@club.Name</MudText>
                                    @if (club.LastRefreshed.HasValue)
                                    {
                                        <MudText Typo="Typo.caption">@club.LastRefreshed.Value.ToString("g")</MudText>
                                    }
                                </MudItem>
                                <MudItem xs="2">
                                    <MudIconButton Icon="@(club.IsFavorite ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarOutline)"
                                                   OnClick="@(e => { e.StopPropagation(); _ = OnToggleFavoriteAsync(club); })"
                                                   role="button"
                                                   aria-label="@(club.IsFavorite ? "unfavorite" : "favorite")" />
                                </MudItem>
                            </MudGrid>
                        </div>
                    </MudListItem>
                }
            </MudList>
            <MudGrid Justify="Justify.Center">
                <MudItem>
                    <MudIconButton Icon="@Icons.Material.Rounded.NavigateBefore"
                                   OnClick="@GoToPrevious"
                                   role="button"
                                   aria-label="previous" />
                    <MudIconButton Icon="@Icons.Material.Rounded.NavigateNext"
                                   OnClick="@GoToNext"
                                   Disabled="@(!HasNextPage)"
                                   role="button"
                                   aria-label="next" />
                </MudItem>
            </MudGrid>
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    [Parameter] public EventCallback<FootballClubModel> OnClubSelected { get; set; }

    public PagedListResultModel<FootballClubModel> Clubs { get; private set; } =
        PagedListResultModel<FootballClubModel>.Empty();

    private bool HasNextPage => Clubs.Total > _currentParameters.Offset + Clubs.Limit;
    private IDisposable? _subscription;
    private PagedListParameters _currentParameters = PagedListParameters.Default();

    protected override async Task OnInitializedAsync()
    {
        _subscription = Client.Subscribe<ClubsRefreshedDashboardEvent>(async () =>
        {
            await LoadClubsAsync();
            StateHasChanged();
        });
        await LoadClubsAsync();
    }

    public async Task LoadClubsAsync()
    {
        Clubs = await Client.GetClubsAsync(_currentParameters);
    }

    private async Task OnRefreshClicked()
    {
        await Client.RefreshClubsAsync();
    }

    private async Task OnToggleFavoriteAsync(FootballClubModel club)
    {
        try
        {
            if (club.IsFavorite)
                await Client.UnfavoriteClubAsync(club.Id);
            else
                await Client.FavoriteClubAsync(club.Id);
            await LoadClubsAsync();
            StateHasChanged();
        }
        catch
        {
            Snackbar.Add("Failed to update favorite status", Severity.Error);
        }
    }

    private async Task GoToNext()
    {
        _currentParameters = new PagedListParameters(
            offset: _currentParameters.Offset + _currentParameters.Limit);
        await LoadClubsAsync();
        StateHasChanged();
    }

    private async Task GoToPrevious()
    {
        _currentParameters = new PagedListParameters(
            offset: Math.Max(0, _currentParameters.Offset - _currentParameters.Limit));
        await LoadClubsAsync();
        StateHasChanged();
    }

    public ValueTask DisposeAsync()
    {
        _subscription?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "ClubsListTests"`
Expected: All tests pass (including new row-click test).

- [ ] **Step 4: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs
git commit -m "feat: ClubsList — add OnClubSelected callback; clickable rows"
```

---

### Task 16: `ClubDetail` component + `Clubs.razor` update

**Files:**
- Create: `src/Personal.Dashboard.Web.Host/Clubs/ClubDetail.razor`
- Modify: `src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor`
- Create: `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubDetailTests.cs`

- [ ] **Step 1: Write `ClubDetailTests`**

Create `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubDetailTests.cs`:

```csharp
using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Clubs;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Clubs;

public class ClubDetailTests
{
    [Fact]
    public void WhenClubIsNullThenShowsPlaceholder()
    {
        using var context = new PersonalDashboardWebContext();

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, null));

        Assert.Contains("Select a club to view details", page.Markup);
    }

    [Fact]
    public void WhenClubIsSetThenShowsClubName()
    {
        using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel();

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));

        Assert.Contains(club.Name, page.Markup);
    }

    [Fact]
    public void WhenClubBelongsToLeagueThenShowsLeagueName()
    {
        using var context = new PersonalDashboardWebContext();
        var leagueName = "Premier League";
        var club = DataFactory.FootballClubModel() with
        {
            Leagues = [new FootballClubLeagueModel(Guid.NewGuid(), leagueName)]
        };

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));

        Assert.Contains(leagueName, page.Markup);
    }

    [Fact]
    public async Task WhenFavoriteToggledThenCallsFavoriteEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        HttpRequestMessage? favoriteRequest = null;
        await context.HttpHandler.SetupFavoriteClub(
            club.Id,
            new ConfigureResponseOptions(Capture: req => favoriteRequest = req)
        );

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(favoriteRequest));
    }
}
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "ClubDetailTests"`
Expected: Build error — `ClubDetail` doesn't exist yet.

- [ ] **Step 3: Create `ClubDetail.razor`**

Create `src/Personal.Dashboard.Web.Host/Clubs/ClubDetail.razor`:

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis
@using MudBlazor

@inject PersonalDashboardApiClient Client
@inject ISnackbar Snackbar

@if (Club is null)
{
    <MudText Typo="Typo.body1" Class="ma-4">Select a club to view details</MudText>
}
else
{
    <MudGrid>
        <MudItem xs="12">
            <MudGrid>
                <MudItem xs="10">
                    <MudText Typo="Typo.h6">@Club.Name</MudText>
                    @if (Club.LastRefreshed.HasValue)
                    {
                        <MudText Typo="Typo.caption">Last refreshed: @Club.LastRefreshed.Value.ToString("g")</MudText>
                    }
                </MudItem>
                <MudItem xs="2">
                    <MudIconButton Icon="@(Club.IsFavorite ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarOutline)"
                                   OnClick="@OnToggleFavoriteAsync"
                                   role="button"
                                   aria-label="@(Club.IsFavorite ? "unfavorite" : "favorite")" />
                </MudItem>
            </MudGrid>
        </MudItem>

        @if (Club.Leagues.Length > 0)
        {
            <MudItem xs="12">
                <MudPaper Class="pa-3">
                    <MudText Typo="Typo.overline">Leagues</MudText>
                    <MudList T="FootballClubLeagueModel">
                        @foreach (var league in Club.Leagues)
                        {
                            <MudListItem>
                                <MudText Typo="Typo.body2">@league.Name</MudText>
                            </MudListItem>
                        }
                    </MudList>
                </MudPaper>
            </MudItem>
        }
    </MudGrid>
}

@code {
    [Parameter] public FootballClubModel? Club { get; set; }
    [Parameter] public EventCallback OnFavoriteChanged { get; set; }

    private async Task OnToggleFavoriteAsync()
    {
        if (Club is null) return;
        try
        {
            if (Club.IsFavorite)
                await Client.UnfavoriteClubAsync(Club.Id);
            else
                await Client.FavoriteClubAsync(Club.Id);
            await OnFavoriteChanged.InvokeAsync();
        }
        catch
        {
            Snackbar.Add("Failed to update favorite status", Severity.Error);
        }
    }
}
```

- [ ] **Step 4: Update `Clubs.razor`**

Replace `src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor`:

```razor
@page "/clubs"

<MudGrid Class="d-flex flex-1">
    <MudItem xs="3" Class="d-flex flex-1">
        <ClubsList OnClubSelected="@(c => { _selected = c; StateHasChanged(); })" @ref="_list" />
    </MudItem>
    <MudItem xs="9">
        <ClubDetail Club="_selected" OnFavoriteChanged="@OnFavoriteChangedAsync" />
    </MudItem>
</MudGrid>

@code {
    private FootballClubModel? _selected;
    private ClubsList? _list;

    private async Task OnFavoriteChangedAsync()
    {
        if (_list is not null)
            await _list.LoadClubsAsync();
        if (_selected is not null && _list is not null)
        {
            _selected = _list.Clubs.Items.FirstOrDefault(c => c.Id == _selected.Id);
        }
        StateHasChanged();
    }
}
```

- [ ] **Step 5: Run all Web.Host.Tests**

Run: `dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Clubs/ClubDetail.razor \
        src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubDetailTests.cs
git commit -m "feat: add ClubDetail component; wire into Clubs page with selection"
```

---

## Chunk 4: Full Test Suite Verification

### Task 17: Run full test suite and fix any remaining failures

- [ ] **Step 1: Build everything**

Run: `dotnet build --configuration Release`
Expected: Zero build errors.

- [ ] **Step 2: Run all non-feature tests**

Run: `dotnet test tests/Personal.Dashboard.Core.Tests tests/Personal.Dashboard.Api.Host.Tests tests/Personal.Dashboard.Web.Host.Tests --configuration Release --no-build`
Expected: All pass.

- [ ] **Step 3: Verify `LeaguesControllerTests.WhenFavoriteLeagueCalledThenReturns204` passes**

The integration test was updated in Task 11 to include a `FootballLeagueSeason`. Confirm it passes in the full run above.

- [ ] **Step 4: Commit any fixes**

If any tests needed tweaks, commit them: `git commit -m "fix: resolve remaining test failures after full suite run"`

---

### Task 18: Feature test updates

**Files:**
- Modify: `tests/Personal.Dashboard.Feature.Tests/Leagues/LeagueFavoritingTests.cs`

The `LeagueFavoritingTests` use Playwright end-to-end. After the data model change, `FavoriteLeagueCommand` only triggers club loading when there's a current season for the league. The Football API populates seasons during `RefreshLeagues`, so the test flow (leagues page → favorite) should still work as long as a league refresh has happened.

Check if `ApplicationFixture` seeds or refreshes leagues with seasons. If it does, no change needed. If not, update the fixture to seed a league with a season.

- [ ] **Step 1: Inspect `ApplicationFixture`**

Read `tests/Personal.Dashboard.Feature.Tests/Fixtures/ApplicationFixture.cs` and check whether leagues are seeded or refreshed.

- [ ] **Step 2: Confirm or update the fixture**

If the fixture calls `POST /leagues/refresh` (which calls the Football API mock), seasons will be populated automatically. If not, add a seed step.

- [ ] **Step 3: Run feature tests locally (if Aspire is available)**

```bash
dotnet test tests/Personal.Dashboard.Feature.Tests --configuration Release
```

Expected: `WhenLeagueFavoritedThenShowsAsFavorite`, `WhenLeagueFavoritedThenClubsAppearInClubsList`, and `WhenClubFavoritedThenShowsAsFavorite` all pass.

- [ ] **Step 4: Commit any fixture changes**

```bash
git add tests/Personal.Dashboard.Feature.Tests/
git commit -m "fix: ensure feature test fixture seeds league seasons for favorite flow"
```

---

### Task 19: Final build and commit

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass (feature tests may be skipped in non-Aspire environments).

- [ ] **Step 2: Final commit if any cleanup needed**

```bash
git add -u
git commit -m "chore: final cleanup after league seasons and detail panels implementation"
```
