# League Country Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Store and display country information (name, code, flag) for each football league using a normalised `FootballCountry` entity with the same alias pattern used for leagues and clubs.

**Architecture:** Introduce `FootballCountry` + `FootballCountryAlias` entities. During `RefreshLeaguesCommand`, countries are looked up by lowercased Football API name alias and upserted, then passed to `FootballLeagueEntity.CreateFromFootballApi` / `UpdateFromFootballApi`. AutoMapper `ProjectTo<>` auto-generates the JOIN so no `.Include()` is needed. The list and detail UI gain flag image + country name display.

**Tech Stack:** .NET 10, EF Core (PostgreSQL/Npgsql), AutoMapper, Blazor WASM, MudBlazor

---

## File Map

| Action | File | Change |
|--------|------|--------|
| Modify | `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiModels.cs` | `FootballApiCountry.Code` and `.Flag` → `string?` |
| Create | `src/Personal.Dashboard.Core/Leagues/Entities/FootballCountry.cs` | `FootballCountry` entity + `FootballCountryAlias` + EF configs |
| Modify | `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs` | Add `CountryId`, `Country` nav; update factory/update signatures |
| Modify | `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs` | Load country aliases dict, upsert countries, pass to entity methods |
| Modify | `src/Personal.Dashboard.Models/FootballModels.cs` | Add `FootballCountryModel` record; add `Country` to `FootballLeagueModel` |
| Modify | `src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs` | Add `FootballCountry → FootballCountryModel` map + `ForMember` on league |
| Create | Migration `AddFootballCountry` | New tables + FK column |
| Modify | `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs` | `Country()` returns nullable `Code`/`Flag` variant when needed |
| Modify | `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs` | `FootballLeague()` factory passes a country |
| Modify | `tests/Personal.Dashboard.Test.Support/DataFactory.cs` | `FootballLeagueModel()` includes a `FootballCountryModel` |
| Modify | `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs` | Update calls to `CreateFromFootballApi` / `UpdateFromFootballApi` to pass country |
| Modify | `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs` | Add country assertion tests |
| Modify | `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesApiTests.cs` | Assert `country.name`, `country.code`, `country.flag` in response |
| Modify | `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor` | Display flag + country name per row |
| Modify | `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor` | Display flag + country name/code in detail panel |
| Modify | `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs` | Add country display tests |
| Modify | `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs` | Add country display tests |

---

## Chunk 1: Data Layer

### Task 1: Make `FootballApiCountry.Code` and `Flag` nullable

**Files:**
- Modify: `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiModels.cs:29`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs:38-45`

- [ ] **Step 1: Change `FootballApiCountry` record**

In `FootballApiModels.cs`, line 29:
```csharp
public record FootballApiCountry(string Name, string? Code, string? Flag);
```

- [ ] **Step 2: Verify the build still compiles**

```bash
dotnet build src/Personal.Dashboard.Core --configuration Release
```
Expected: 0 errors (existing `FootballApiDataFactory.Country()` passes positional args — `Code` and `Flag` are now nullable so the literal strings remain valid).

- [ ] **Step 3: Commit**

```bash
git add src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiModels.cs
git commit -m "feat: make FootballApiCountry.Code and Flag nullable"
```

---

### Task 2: `FootballCountry` + `FootballCountryAlias` entities

**Files:**
- Create: `src/Personal.Dashboard.Core/Leagues/Entities/FootballCountry.cs`

- [ ] **Step 1: Write the failing entity tests**

Create `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballCountryTests.cs`:
```csharp
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Entities;

public class FootballCountryTests
{
    [Fact]
    public void WhenCreateFromFootballApiCalledThenStoresNameCodeFlag()
    {
        var apiCountry = FootballApiDataFactory.Country();

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Equal(apiCountry.Name, country.Name);
        Assert.Equal(apiCountry.Code, country.Code);
        Assert.Equal(apiCountry.Flag, country.Flag);
    }

    [Fact]
    public void WhenCreateFromFootballApiCalledThenAddsLowercasedAlias()
    {
        var apiCountry = FootballApiDataFactory.Country() with { Name = "England" };

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Single(country.Aliases, a =>
            a.AliasSource == DataSource.FootballApi && a.Alias == "england");
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenUpdatesNameCodeFlag()
    {
        var country = FootballCountry.CreateFromFootballApi(FootballApiDataFactory.Country());
        var updated = FootballApiDataFactory.Country();

        country.UpdateFromFootballApi(updated);

        Assert.Equal(updated.Name, country.Name);
        Assert.Equal(updated.Code, country.Code);
        Assert.Equal(updated.Flag, country.Flag);
    }

    [Fact]
    public void WhenCountryHasNullCodeAndFlagThenCreateFromFootballApiSucceeds()
    {
        var apiCountry = new FootballApiCountry("World", null, null);

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Null(country.Code);
        Assert.Null(country.Flag);
        Assert.Single(country.Aliases, a => a.Alias == "world");
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --filter "FullyQualifiedName~FootballCountryTests" --configuration Release
```
Expected: FAIL with type not found.

- [ ] **Step 3: Create `FootballCountry.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballCountry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Flag { get; set; }

    public ICollection<FootballCountryAlias> Aliases { get; set; } = new List<FootballCountryAlias>();
    public ICollection<FootballLeagueEntity> Leagues { get; set; } = new List<FootballLeagueEntity>();

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballCountryAlias
        {
            AliasSource = source,
            Alias = alias,
            Country = this,
        });
    }

    public static FootballCountry CreateFromFootballApi(FootballApiCountry apiCountry)
    {
        var entity = new FootballCountry();
        entity.AddAlias(DataSource.FootballApi, apiCountry.Name.ToLowerInvariant());
        entity.UpdateFromFootballApi(apiCountry);
        return entity;
    }

    public void UpdateFromFootballApi(FootballApiCountry apiCountry)
    {
        Name = apiCountry.Name;
        Code = apiCountry.Code;
        Flag = apiCountry.Flag;
    }
}

public class FootballCountryConfiguration : IEntityTypeConfiguration<FootballCountry>
{
    public void Configure(EntityTypeBuilder<FootballCountry> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Code);
        builder.Property(c => c.Flag);

        builder.HasMany(c => c.Aliases)
            .WithOne(a => a.Country)
            .HasForeignKey(a => a.CountryId);
    }
}

public class FootballCountryAlias
{
    public string Alias { get; set; } = "";
    public string AliasSource { get; set; } = "";
    public Guid CountryId { get; set; } = Guid.Empty;

    public required FootballCountry Country { get; set; }
}

public class FootballCountryAliasConfiguration : IEntityTypeConfiguration<FootballCountryAlias>
{
    public void Configure(EntityTypeBuilder<FootballCountryAlias> builder)
    {
        builder.Property(a => a.Alias).IsRequired();
        builder.Property(a => a.AliasSource).IsRequired();

        builder.HasKey(a => new
        {
            a.AliasSource,
            a.Alias,
            a.CountryId
        });

        // Relationship is configured from FootballCountryConfiguration.HasMany(...)
        // Do not configure HasOne/WithMany here to avoid duplicate registration.
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --filter "FullyQualifiedName~FootballCountryTests" --configuration Release
```
Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Entities/FootballCountry.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballCountryTests.cs
git commit -m "feat: add FootballCountry and FootballCountryAlias entities"
```

---

### Task 3: Add `CountryId` / `Country` to `FootballLeagueEntity`

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs`

- [ ] **Step 1: Write the failing tests**

In `FootballLeagueEntityTests.cs`, add two new tests (the existing `WhenUpdateFromFootballApiCalledThenSetsNameAndLastRefreshed` test will also need updating since `UpdateFromFootballApi` gains a `FootballCountry` parameter — update the existing test at the same time):

```csharp
[Fact]
public void WhenCreateFromFootballApiCalledThenSetsCountry()
{
    var apiLeague = FootballApiDataFactory.League();
    var country = FootballCountry.CreateFromFootballApi(apiLeague.Country);

    var entity = FootballLeagueEntity.CreateFromFootballApi(apiLeague, country);

    Assert.Equal(country, entity.Country);
}

[Fact]
public void WhenUpdateFromFootballApiCalledThenUpdatesCountry()
{
    var apiLeague = FootballApiDataFactory.League();
    var originalCountry = FootballCountry.CreateFromFootballApi(apiLeague.Country);
    var entity = FootballLeagueEntity.CreateFromFootballApi(apiLeague, originalCountry);

    var newCountry = FootballCountry.CreateFromFootballApi(FootballApiDataFactory.Country());
    entity.UpdateFromFootballApi(FootballApiDataFactory.League(), newCountry);

    Assert.Equal(newCountry, entity.Country);
}
```

Also update the two existing tests that call `UpdateFromFootballApi` — they need a country argument. Pass `FootballCountry.CreateFromFootballApi(apiLeague.Country)` as the second argument.

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --filter "FullyQualifiedName~FootballLeagueEntityTests" --configuration Release
```
Expected: FAIL — method signatures don't match yet.

- [ ] **Step 3: Update `FootballLeagueEntity`**

Add properties and update method signatures in `FootballLeagueEntity.cs`:
```csharp
public Guid? CountryId { get; set; }
public FootballCountry? Country { get; set; }
```

Change factory and update methods:
```csharp
public static FootballLeagueEntity CreateFromFootballApi(FootballApiLeague apiLeague, FootballCountry country)
{
    var entity = new FootballLeagueEntity();
    entity.AddAlias(DataSource.FootballApi, $"{apiLeague.League.Id}");
    entity.UpdateFromFootballApi(apiLeague, country);
    return entity;
}

public void UpdateFromFootballApi(FootballApiLeague apiLeague, FootballCountry country)
{
    Name = apiLeague.League.Name;
    LastRefreshed = DateTimeOffset.UtcNow;
    Country = country;
    UpsertSeasons(apiLeague.Seasons);
}
```

Also add the FK relationship to `FootballLeagueEntityConfiguration`:
```csharp
builder.HasOne(p => p.Country)
    .WithMany(c => c.Leagues)
    .HasForeignKey(p => p.CountryId)
    .IsRequired(false);
```

- [ ] **Step 4: Update `RefreshLeaguesCommand.cs` to compile with new signatures**

`RefreshLeaguesCommand.cs` currently calls `CreateFromFootballApi(apiLeague)` and `UpdateFromFootballApi(apiLeague)` — both no longer exist. To keep the codebase compilable (critical for the build to stay green while Task 5 adds the full country-upsert logic), update these call sites now with inline country creation. This is intentionally temporary and will be replaced by proper alias-lookup in Task 5:

```csharp
if (existingAliases.TryGetValue(aliasKey, out var alias))
    alias.League.UpdateFromFootballApi(apiLeague, FootballCountry.CreateFromFootballApi(apiLeague.Country));
else
    context.Add(FootballLeagueEntity.CreateFromFootballApi(apiLeague, FootballCountry.CreateFromFootballApi(apiLeague.Country)));
```

Note: This creates duplicate country entities on every call. Task 5 replaces this with the correct alias-based lookup.

- [ ] **Step 5: Update `PersonalDashboardEntityFactory.FootballLeague()`**

The factory creates a bare `FootballLeagueEntity` directly (not via `CreateFromFootballApi`), so no signature change needed there — the entity just gains two new nullable properties with no required value.

- [ ] **Step 6: Run full build to verify everything compiles**

```bash
dotnet build --configuration Release
```
Expected: 0 errors.

- [ ] **Step 7: Run entity tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --filter "FullyQualifiedName~FootballLeagueEntityTests" --configuration Release
```
Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs \
        src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs
git commit -m "feat: add CountryId/Country to FootballLeagueEntity (temporary inline country creation)"
```

---

### Task 4: EF Core migration `AddFootballCountry`

**Files:**
- Create: migration via `dotnet ef migrations add`

- [ ] **Step 1: Register new entity configurations in `PersonalDashboardContext`**

Find `PersonalDashboardContext` (likely in `src/Personal.Dashboard.Core/Common/Storage/`). Apply configs are registered by scanning assemblies or individually. Check how existing configs are registered and do the same for `FootballCountryConfiguration` and `FootballCountryAliasConfiguration`.

```bash
grep -r "FootballLeagueAliasConfiguration\|ApplyConfigurationsFromAssembly\|modelBuilder.ApplyConfiguration" \
  src/Personal.Dashboard.Core/Common/Storage/
```

If using `ApplyConfigurationsFromAssembly`, the new configs are auto-discovered and no change is needed. If registered individually, add them.

- [ ] **Step 2: Add the migration**

```bash
dotnet ef migrations add AddFootballCountry \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```
Expected: Migration file created under `src/Personal.Dashboard.Migrations.Host/Migrations/`.

- [ ] **Step 3: Verify migration contents**

Open the generated migration and confirm it:
- Creates `FootballCountry` table with `Id`, `Name`, `Code` (nullable), `Flag` (nullable)
- Creates `FootballCountryAlias` table with composite PK `(AliasSource, Alias, CountryId)` and FK to `FootballCountry`
- Adds `CountryId` nullable column to the leagues table

- [ ] **Step 4: Run full core tests to confirm nothing is broken**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release
```
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Migrations.Host/Migrations/ \
        src/Personal.Dashboard.Core/
git commit -m "feat: add AddFootballCountry migration"
```

---

## Chunk 2: Command + Query + Mapping

### Task 5: `RefreshLeaguesCommandHandler` — upsert countries

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `RefreshLeaguesCommandTests.cs`:
```csharp
[Fact]
public async Task WhenRefreshingLeaguesThenCreatesCountryForEachLeague()
{
    var apiLeague = FootballApiDataFactory.League();
    await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

    var countries = await _context.Set<FootballCountry>()
        .Include(c => c.Aliases)
        .ToArrayAsync();
    Assert.Single(countries);
    Assert.Equal(apiLeague.Country.Name, countries[0].Name);
    Assert.Single(countries[0].Aliases, a =>
        a.AliasSource == DataSource.FootballApi &&
        a.Alias == apiLeague.Country.Name.ToLowerInvariant());
}

[Fact]
public async Task WhenRefreshingLeaguesTwiceThenReusesExistingCountry()
{
    var apiLeague = FootballApiDataFactory.League();
    await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());
    await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);
    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

    var countries = await _context.Set<FootballCountry>().ToArrayAsync();
    Assert.Single(countries);
}

[Fact]
public async Task WhenRefreshingLeaguesThenAssignsCountryToLeague()
{
    var apiLeague = FootballApiDataFactory.League();
    await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

    var league = await _context.Set<FootballLeagueEntity>()
        .Include(l => l.Country)
        .SingleAsync();
    Assert.NotNull(league.Country);
    Assert.Equal(apiLeague.Country.Name, league.Country.Name);
}

[Fact]
public async Task WhenLeagueHasNullCountryCodeAndFlagThenRefreshSucceeds()
{
    var apiLeague = FootballApiDataFactory.League() with
    {
        Country = new FootballApiCountry("World", null, null)
    };
    await _handler.SetupGetLeagues(BaseUrl, [apiLeague]);

    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

    var league = await _context.Set<FootballLeagueEntity>()
        .Include(l => l.Country)
        .SingleAsync();
    Assert.NotNull(league.Country);
    Assert.Null(league.Country.Code);
    Assert.Null(league.Country.Flag);
}
```

- [ ] **Step 2: Run tests to confirm new tests fail and existing tests still pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests \
  --filter "FullyQualifiedName~RefreshLeaguesCommandTests" --configuration Release
```
Expected: the four new country tests FAIL; all existing tests PASS (they were already updated in Task 3 to compile with the new signature, and the temporary inline country creation satisfies them).

- [ ] **Step 3: Update `RefreshLeaguesCommandHandler.Handle`**

After loading `existingAliases`, add country alias loading:
```csharp
var existingCountryAliases = await context.Set<FootballCountryAlias>()
    .Where(a => a.AliasSource == DataSource.FootballApi)
    .Include(a => a.Country)
    .ToDictionaryAsync(a => a.Alias, cancellationToken);
```

In the `foreach` loop, resolve the country before creating/updating the league:
```csharp
foreach (var apiLeague in response.Response)
{
    var countryKey = apiLeague.Country.Name.ToLowerInvariant();
    FootballCountry country;
    if (existingCountryAliases.TryGetValue(countryKey, out var countryAlias))
    {
        country = countryAlias.Country;
        country.UpdateFromFootballApi(apiLeague.Country);
    }
    else
    {
        country = FootballCountry.CreateFromFootballApi(apiLeague.Country);
        context.Add(country);
        existingCountryAliases[countryKey] = country.Aliases
            .First(a => a.AliasSource == DataSource.FootballApi);
    }

    var aliasKey = $"{apiLeague.League.Id}";
    if (existingAliases.TryGetValue(aliasKey, out var alias))
        alias.League.UpdateFromFootballApi(apiLeague, country);
    else
        context.Add(FootballLeagueEntity.CreateFromFootballApi(apiLeague, country));
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests \
  --filter "FullyQualifiedName~RefreshLeaguesCommandTests" --configuration Release
```
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs
git commit -m "feat: upsert FootballCountry during RefreshLeaguesCommand"
```

---

### Task 6: Shared model + AutoMapper

**Files:**
- Modify: `src/Personal.Dashboard.Models/FootballModels.cs`
- Modify: `src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs`
- Modify: `tests/Personal.Dashboard.Test.Support/DataFactory.cs`

- [ ] **Step 1: Add `FootballCountryModel` and update `FootballLeagueModel`**

In `FootballModels.cs`:
```csharp
public record FootballCountryModel(string Name, string? Code, string? Flag);

public record FootballLeagueModel(
    Guid Id,
    string Name,
    FootballCountryModel? Country,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballLeagueSeasonModel[] Seasons);
```

- [ ] **Step 2: Update `LeaguesMappingProfile`**

```csharp
public LeaguesMappingProfile()
{
    CreateMap<FootballLeagueSeason, FootballLeagueSeasonModel>();
    CreateMap<FootballCountry, FootballCountryModel>();
    CreateMap<FootballLeagueEntity, FootballLeagueModel>()
        .ForMember(d => d.Seasons, o => o.MapFrom(s => s.Seasons))
        .ForMember(d => d.Country, o => o.MapFrom(s => s.Country));
}
```

- [ ] **Step 3: Update `DataFactory.FootballLeagueModel()`**

`FootballLeagueModel` is now a positional record with `Country` as the third parameter. Update `DataFactory.cs`:
```csharp
public static FootballCountryModel FootballCountryModel()
{
    return new FootballCountryModel(
        Faker.Address.Country(),
        Faker.Address.CountryCode(),
        Faker.Internet.Url()
    );
}

public static FootballLeagueModel FootballLeagueModel()
{
    return new FootballLeagueModel(
        Faker.Random.Guid(),
        Faker.Company.CompanyName(),
        FootballCountryModel(),
        Faker.Date.RecentOffset(),
        Faker.Random.Bool(),
        [new FootballLeagueSeasonModel(Faker.Random.Int(2020, 2025), true)]
    );
}
```

- [ ] **Step 4: Build everything to catch broken call sites**

```bash
dotnet build --configuration Release
```
Expected: 0 errors. (The `with` expressions in tests that set individual properties on `FootballLeagueModel` using named syntax will continue to work since records support named `with` initialization — positional record parameters are also named properties.)

- [ ] **Step 5: Run all tests**

```bash
dotnet test --configuration Release --no-build
```
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Models/FootballModels.cs \
        src/Personal.Dashboard.Core/Leagues/Mappers/LeaguesMappingProfile.cs \
        tests/Personal.Dashboard.Test.Support/DataFactory.cs
git commit -m "feat: add FootballCountryModel and wire AutoMapper for country"
```

---

### Task 7: API integration test for country fields

**Files:**
- Modify: `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesApiTests.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs`

- [ ] **Step 1: Add a country to `PersonalDashboardEntityFactory.FootballLeague()`**

The factory builds entities directly. Attach a country so API tests get full data:
```csharp
public static FootballCountry FootballCountry(Action<FootballCountry>? configure = null)
{
    var entity = new FootballCountry
    {
        Name = Faker.Address.Country(),
        Code = Faker.Address.CountryCode(),
        Flag = Faker.Internet.Url(),
    };
    configure?.Invoke(entity);
    return entity;
}

public static FootballLeagueEntity FootballLeague(Action<FootballLeagueEntity>? configure = null)
{
    var country = FootballCountry();
    var entity = new FootballLeagueEntity
    {
        Name = Faker.Company.CompanyName(),
        Id = Faker.Random.Guid(),
        Country = country,
    };
    configure?.Invoke(entity);
    return entity;
}
```

- [ ] **Step 2: Write the failing API test**

Add to `LeaguesApiTests.cs`:
```csharp
[Fact]
public async Task WhenGettingLeaguesThenReturnsCountryForEachLeague()
{
    var league = PersonalDashboardEntityFactory.FootballLeague();
    await app.AddToDbAsync(league);

    var response = await _client.GetAsync("/leagues");
    var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballLeagueModel>>();

    var item = result?.Items.Single(l => l.Id == league.Id);
    Assert.NotNull(item?.Country);
    Assert.Equal(league.Country?.Name, item?.Country?.Name);
    Assert.Equal(league.Country?.Code, item?.Country?.Code);
    Assert.Equal(league.Country?.Flag, item?.Country?.Flag);
}
```

- [ ] **Step 3: Run the new test**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests \
  --filter "WhenGettingLeaguesThenReturnsCountryForEachLeague" --configuration Release
```
Expected: PASS — by this point the migration (Task 4), AutoMapper mapping (Task 6), and entity factory (this task) are all in place, so the test validates the full stack end-to-end immediately.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test --configuration Release
```
Expected: all PASS (including the new API test once migration runs in the test fixture).

- [ ] **Step 5: Commit**

```bash
git add tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesApiTests.cs
git commit -m "test: assert country fields returned from GET /leagues"
```

---

## Chunk 3: UI

### Task 8: `LeaguesList.razor` — flag + country name

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`

- [ ] **Step 1: Write the failing web component tests**

Add to `LeaguesListTests.cs`:
```csharp
[Fact]
public async Task WhenLeagueHasCountryThenDisplaysCountryName()
{
    await using var context = new PersonalDashboardWebContext();
    var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
    var league = DataFactory.FootballLeagueModel() with { Country = country };
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<LeaguesList>();

    await Eventually.Assert(() => Assert.Contains("England", page.Markup));
}

[Fact]
public async Task WhenLeagueHasCountryFlagThenDisplaysFlagImage()
{
    await using var context = new PersonalDashboardWebContext();
    var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
    var league = DataFactory.FootballLeagueModel() with { Country = country };
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<LeaguesList>();

    await Eventually.Assert(() =>
        Assert.Contains("https://flags.example.com/gb.svg", page.Markup));
}

[Fact]
public async Task WhenLeagueHasNullCountryThenDoesNotRenderFlagImage()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel() with { Country = null };
    await context.HttpHandler.SetupLeagues(leagues: [league]);

    var page = context.Render<LeaguesList>();

    await Eventually.Assert(() =>
        Assert.DoesNotContain("<img", page.Markup));
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests \
  --filter "FullyQualifiedName~LeaguesListTests" --configuration Release
```
Expected: new tests FAIL.

- [ ] **Step 3: Update `LeaguesList.razor`**

Inside the `<div role="button">` block, after `<MudText Typo="Typo.body1">@league.Name</MudText>`, add:
```razor
@if (league.Country is not null)
{
    @if (!string.IsNullOrEmpty(league.Country.Flag))
    {
        <img src="@league.Country.Flag" style="height:20px" alt="@league.Country.Name flag" />
    }
    <MudText Typo="Typo.caption">@league.Country.Name</MudText>
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests \
  --filter "FullyQualifiedName~LeaguesListTests" --configuration Release
```
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs
git commit -m "feat: display country flag and name in LeaguesList"
```

---

### Task 9: `LeagueDetail.razor` — country section

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `LeagueDetailTests.cs`:
```csharp
[Fact]
public async Task WhenLeagueHasCountryThenShowsCountryNameAndCode()
{
    await using var context = new PersonalDashboardWebContext();
    var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
    var league = DataFactory.FootballLeagueModel() with { Country = country };

    var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

    Assert.Contains("England", page.Markup);
    Assert.Contains("GB", page.Markup);
}

[Fact]
public async Task WhenLeagueHasCountryFlagThenShowsFlagImage()
{
    await using var context = new PersonalDashboardWebContext();
    var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
    var league = DataFactory.FootballLeagueModel() with { Country = country };

    var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

    Assert.Contains("https://flags.example.com/gb.svg", page.Markup);
}

[Fact]
public async Task WhenLeagueHasNullCountryThenDoesNotShowCountrySection()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel() with { Country = null };

    var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

    Assert.DoesNotContain("<img", page.Markup);
}

[Fact]
public async Task WhenLeagueCountryHasNullCodeThenShowsOnlyName()
{
    await using var context = new PersonalDashboardWebContext();
    var country = new FootballCountryModel("World", null, null);
    var league = DataFactory.FootballLeagueModel() with { Country = country };

    var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

    Assert.Contains("World", page.Markup);
    Assert.DoesNotContain("·", page.Markup);
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests \
  --filter "FullyQualifiedName~LeagueDetailTests" --configuration Release
```
Expected: new tests FAIL.

- [ ] **Step 3: Update `LeagueDetail.razor`**

After `<MudText Typo="Typo.h6">@League.Name</MudText>`, add:
```razor
@if (League.Country is not null)
{
    <MudItem xs="12">
        <MudGrid AlignItems="AlignItems.Center">
            @if (!string.IsNullOrEmpty(League.Country.Flag))
            {
                <MudItem>
                    <img src="@League.Country.Flag" style="height:20px" alt="@League.Country.Name flag" />
                </MudItem>
            }
            <MudItem>
                <MudText Typo="Typo.body2">
                    @League.Country.Name@(League.Country.Code is not null ? $" · {League.Country.Code}" : "")
                </MudText>
            </MudItem>
        </MudGrid>
    </MudItem>
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests \
  --filter "FullyQualifiedName~LeagueDetailTests" --configuration Release
```
Expected: all PASS.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test --configuration Release
```
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Leagues/LeagueDetail.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeagueDetailTests.cs
git commit -m "feat: display country section in LeagueDetail"
```

---

### Task 10: Final verification

- [ ] **Step 1: Full clean build**

```bash
dotnet build --configuration Release
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Full test suite**

```bash
dotnet test --configuration Release --no-build
```
Expected: all PASS.

- [ ] **Step 3: Use `superpowers:finishing-a-development-branch` to complete the work**
