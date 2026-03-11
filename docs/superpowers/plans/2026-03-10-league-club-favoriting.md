# League Favoriting + Clubs Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add league/club favoriting, a new Clubs domain entity with full CQRS, Football API team integration, and a Clubs UI page with SignalR auto-refresh.

**Architecture:** Clubs mirror leagues — `FootballClubEntity` with EF config, alias table, and many-to-many join to leagues. CQRS handlers (Favorite/Unfavorite/Refresh/Get) follow the same patterns as leagues. A `RefreshClubsCommand` handler extracts one shared `ProcessTeams` method; the with-LeagueId and without-LeagueId paths differ only in how they acquire the `FootballApiTeam[]` collection. Entity methods own all state mutations including alias additions. Commands throw `EntityNotFoundException` when a requested entity is not found; `CqrsController` catches it and returns 404.

**Tech Stack:** .NET 10, EF Core + PostgreSQL, MediatR, AutoMapper, MudBlazor, Aspire, bUnit, Playwright

---

## Chunk 1: Foundation — Entities, Models, Migrations

### Task 1: EntityNotFoundException + CqrsController 404 handling

**Files:**
- Create: `src/Personal.Dashboard.Core/Common/Exceptions/EntityNotFoundException.cs`
- Modify: `src/Personal.Dashboard.Api.Host/Common/CqrsController.cs`
- Create: `tests/Personal.Dashboard.Api.Host.Tests/Common/CqrsControllerNotFoundTests.cs`

- [ ] **Step 1: Write a failing test**

Create `tests/Personal.Dashboard.Api.Host.Tests/Common/CqrsControllerNotFoundTests.cs`:

```csharp
using System.Net;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Api.Host.Tests.Common;

public class CqrsControllerNotFoundTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenFavoringNonExistentLeagueThenReturnsNotFound()
    {
        var response = await _client.PostAsync($"/leagues/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release --filter "CqrsControllerNotFoundTests"
```

Expected: FAIL — returns something other than 404.

- [ ] **Step 3: Create EntityNotFoundException**

Create `src/Personal.Dashboard.Core/Common/Exceptions/EntityNotFoundException.cs`:

```csharp
namespace Personal.Dashboard.Core.Common.Exceptions;

public class EntityNotFoundException(Type entityType, object id)
    : Exception($"{entityType.Name} with id '{id}' was not found.");
```

- [ ] **Step 4: Update CqrsController to catch EntityNotFoundException**

In `src/Personal.Dashboard.Api.Host/Common/CqrsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Exceptions;

namespace Personal.Dashboard.Api.Host.Common;

public abstract class CqrsController(ICqrsBus cqrsBus) : ControllerBase
{
    protected async Task<IActionResult> QueryAsync<TResult>(IQuery<TResult> query, int statusCode = 200)
    {
        try
        {
            var result = await cqrsBus.QueryAsync(query).ConfigureAwait(false);
            return StatusCode(statusCode, result);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    protected async Task<IActionResult> ExecuteAsync<TCommand>(TCommand command, int statusCode = 200)
        where TCommand : ICommand
    {
        try
        {
            await cqrsBus.ExecuteAsync(command).ConfigureAwait(false);
            return StatusCode(statusCode, command);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    protected async Task<IActionResult> ExecuteAsync<TResult>(ICommand<TResult> query, int statusCode = 200)
    {
        try
        {
            var result = await cqrsBus.ExecuteAsync(query).ConfigureAwait(false);
            return StatusCode(statusCode, result);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
```

The test for `FavoringNonExistentLeague` will pass once `FavoriteLeagueCommand` is implemented in Task 8 and throws `EntityNotFoundException` for a missing league. For now, build and verify no compilation errors.

- [ ] **Step 5: Build to verify compilation**

```bash
dotnet build --configuration Release
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Common/Exceptions/ \
        src/Personal.Dashboard.Api.Host/Common/CqrsController.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Common/CqrsControllerNotFoundTests.cs
git commit -m "feat: add EntityNotFoundException and 404 handling in CqrsController"
```

---

### Task 2: Add IsFavorite and behavior to FootballLeagueEntity + first migration

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`
- Modify: `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`
- Create: EF migration `AddIsFavoriteToLeague`
- Create: `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs`:

```csharp
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Entities;

public class FootballLeagueEntityTests
{
    [Fact]
    public void WhenFavoriteCalledThenIsFavoriteIsTrue()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Favorite();
        Assert.True(league.IsFavorite);
    }

    [Fact]
    public void WhenUnfavoriteCalledThenIsFavoriteIsFalse()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Favorite();
        league.Unfavorite();
        Assert.False(league.IsFavorite);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenSetsNameAndLastRefreshed()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        var apiLeague = FootballApiDataFactory.League();

        league.UpdateFromFootballApi(apiLeague);

        Assert.Equal(apiLeague.League.Name, league.Name);
        Assert.NotNull(league.LastRefreshed);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FootballLeagueEntityTests"
```

Expected: FAIL — `IsFavorite`, `Favorite()`, `Unfavorite()`, `UpdateFromFootballApi()` not found.

- [ ] **Step 3: Implement entity changes**

In `src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueEntity
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = "";
    public DateTimeOffset? LastRefreshed { get; set; }
    public bool IsFavorite { get; set; }

    public ICollection<FootballLeagueAlias> Aliases { get; set; } = new List<FootballLeagueAlias>();
    public ICollection<FootballClubEntity> Clubs { get; set; } = new List<FootballClubEntity>();

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

Note: `FootballClubEntity` is forward-referenced and will be added in Task 3. The many-to-many join table is configured in `FootballClubEntityConfiguration`; EF discovers the `Clubs` nav automatically.

- [ ] **Step 4: Update RefreshLeaguesCommand to call UpdateFromFootballApi**

In `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`, replace the direct property assignments inside the foreach loop with `UpdateFromFootballApi`. Remove the `now` local variable:

```csharp
foreach (var apiLeague in response.Response)
{
    var aliasKey = $"{apiLeague.League.Id}";
    if (existingAliases.TryGetValue(aliasKey, out var alias))
    {
        alias.League.UpdateFromFootballApi(apiLeague);
    }
    else
    {
        var entity = new FootballLeagueEntity();
        entity.AddAlias(DataSource.FootballApi, aliasKey);
        entity.UpdateFromFootballApi(apiLeague);
        context.Add(entity);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release
```

Expected: PASS

- [ ] **Step 6: Generate first migration**

```bash
dotnet ef migrations add AddIsFavoriteToLeague \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```

- [ ] **Step 7: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Entities/FootballLeagueEntity.cs \
        src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Entities/FootballLeagueEntityTests.cs \
        src/Personal.Dashboard.Migrations.Host/
git commit -m "feat: add IsFavorite and UpdateFromFootballApi to FootballLeagueEntity"
```

---

### Task 3: FootballClubAlias + FootballClubEntity + second migration

**Files:**
- Create: `src/Personal.Dashboard.Core/Clubs/Entities/FootballClubAlias.cs`
- Create: `src/Personal.Dashboard.Core/Clubs/Entities/FootballClubEntity.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Clubs/Entities/FootballClubEntityTests.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Clubs/Entities/FootballClubEntityTests.cs`:

```csharp
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Entities;

public class FootballClubEntityTests
{
    [Fact]
    public void WhenFavoriteCalledThenIsFavoriteIsTrue()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.Favorite();
        Assert.True(club.IsFavorite);
    }

    [Fact]
    public void WhenUnfavoriteCalledThenIsFavoriteIsFalse()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.Favorite();
        club.Unfavorite();
        Assert.False(club.IsFavorite);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenSetsNameAndLastRefreshed()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        var team = FootballApiDataFactory.Team();

        club.UpdateFromFootballApi(team);

        Assert.Equal(team.Team.Name, club.Name);
        Assert.NotNull(club.LastRefreshed);
    }

    [Fact]
    public void WhenAddAliasThenAliasIsAdded()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.AddAlias(DataSource.FootballApi, "42");
        Assert.Single(club.Aliases);
        Assert.Equal("42", club.Aliases.First().Alias);
    }

    [Fact]
    public void WhenAddLeagueThenLeagueIsAdded()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        var league = PersonalDashboardEntityFactory.FootballLeague();
        club.AddLeague(league);
        Assert.Single(club.Leagues);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FootballClubEntityTests"
```

Expected: FAIL — types not found.

- [ ] **Step 3: Create FootballClubAlias**

Create `src/Personal.Dashboard.Core/Clubs/Entities/FootballClubAlias.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Personal.Dashboard.Core.Clubs.Entities;

public class FootballClubAlias
{
    public string Alias { get; set; } = "";
    public string AliasSource { get; set; } = "";
    public Guid ClubId { get; set; } = Guid.Empty;

    public required FootballClubEntity Club { get; set; }
}

public class FootballClubAliasConfiguration : IEntityTypeConfiguration<FootballClubAlias>
{
    public void Configure(EntityTypeBuilder<FootballClubAlias> builder)
    {
        builder.Property(a => a.Alias).IsRequired();
        builder.Property(a => a.AliasSource).IsRequired();

        builder.HasOne(a => a.Club)
            .WithMany(c => c.Aliases)
            .HasForeignKey(a => a.ClubId);

        builder.HasKey(a => new { a.AliasSource, a.Alias, a.ClubId });
    }
}
```

- [ ] **Step 4: Create FootballClubEntity**

Create `src/Personal.Dashboard.Core/Clubs/Entities/FootballClubEntity.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Entities;

public class FootballClubEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastRefreshed { get; set; }

    public ICollection<FootballLeagueEntity> Leagues { get; set; } = new List<FootballLeagueEntity>();
    public ICollection<FootballClubAlias> Aliases { get; set; } = new List<FootballClubAlias>();

    public void Favorite() => IsFavorite = true;
    public void Unfavorite() => IsFavorite = false;

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballClubAlias { AliasSource = source, Alias = alias, Club = this });
    }

    public void AddLeague(FootballLeagueEntity league)
    {
        Leagues.Add(league);
    }

    public void UpdateFromFootballApi(FootballApiTeam team)
    {
        Name = team.Team.Name;
        LastRefreshed = DateTimeOffset.UtcNow;
    }
}

public class FootballClubEntityConfiguration : IEntityTypeConfiguration<FootballClubEntity>
{
    public void Configure(EntityTypeBuilder<FootballClubEntity> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired();

        builder.HasMany(c => c.Aliases)
            .WithOne(a => a.Club)
            .HasForeignKey(a => a.ClubId);

        builder.HasMany(c => c.Leagues)
            .WithMany(l => l.Clubs)
            .UsingEntity(j => j.ToTable("FootballLeagueClub"));
    }
}
```

- [ ] **Step 5: Add factory helpers to PersonalDashboardEntityFactory**

In `tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs`, add:

```csharp
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;

// inside PersonalDashboardEntityFactory class:
public static FootballClubEntity FootballClub(Action<FootballClubEntity>? configure = null)
{
    var entity = new FootballClubEntity
    {
        Id = Faker.Random.Guid(),
        Name = Faker.Company.CompanyName(),
    };
    configure?.Invoke(entity);
    return entity;
}

public static (FootballLeagueEntity League, FootballClubEntity Club) FootballClubInLeague(
    string leagueApiAlias = "123",
    string clubApiAlias = "456")
{
    var league = FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
    var club = FootballClub();
    club.AddAlias(DataSource.FootballApi, clubApiAlias);
    club.AddLeague(league);
    return (league, club);
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FootballClubEntityTests"
```

Expected: PASS

- [ ] **Step 7: Generate second migration**

```bash
dotnet ef migrations add AddFootballClub \
  --project src/Personal.Dashboard.Migrations.Host \
  --startup-project src/Personal.Dashboard.Migrations.Host
```

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/ \
        tests/Personal.Dashboard.Core.Tests/Clubs/Entities/ \
        tests/Personal.Dashboard.Core.Tests/Support/PersonalDashboardEntityFactory.cs \
        src/Personal.Dashboard.Migrations.Host/
git commit -m "feat: add FootballClubEntity with alias, league nav, and entity methods"
```

---

### Task 4: Football API team models + GetTeamsAsync

**Files:**
- Modify: `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiModels.cs`
- Modify: `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiClient.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs`

- [ ] **Step 1: Add team models to FootballApiModels.cs**

At the end of `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiModels.cs`, append:

```csharp
public record FootballApiTeamInfo(long Id, string Name);

public record FootballApiTeam(FootballApiTeamInfo Team);

public record FootballApiTeamsParameters(
    long? League = null,
    long? Season = null
) : FootballApiParameters(new Dictionary<string, object?>
{
    { "league", League },
    { "season", Season }
});
```

- [ ] **Step 2: Add GetTeamsAsync to IFootballApiClient and FootballApiClient**

In `src/Personal.Dashboard.Core/Common/Apis/FootballApi/FootballApiClient.cs`:

```csharp
// add to IFootballApiClient:
Task<FootballApiResponse<FootballApiTeamsParameters, FootballApiTeam[]>> GetTeamsAsync(
    FootballApiTeamsParameters parameters
);

// add to FootballApiClient:
public async Task<FootballApiResponse<FootballApiTeamsParameters, FootballApiTeam[]>> GetTeamsAsync(
    FootballApiTeamsParameters parameters)
{
    return await GetAsync<FootballApiTeamsParameters, FootballApiTeam[]>("/teams", parameters);
}
```

- [ ] **Step 3: Add Team factory methods**

In `tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs`, add:

```csharp
public static FootballApiTeamInfo TeamInfo()
{
    return new FootballApiTeamInfo(Faker.Random.Long(1, 99999), Faker.Company.CompanyName());
}

public static FootballApiTeam Team()
{
    return new FootballApiTeam(TeamInfo());
}
```

- [ ] **Step 4: Add SetupGetTeams extension method**

In `tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs`, add:

```csharp
public static async Task SetupGetTeams(
    this FakeHttpMessageHandler handler,
    string baseUrl,
    long leagueId,
    FootballApiTeam[] teams)
{
    await handler.SetupGetJsonResponseAsync(
        $"{baseUrl}/teams?league={leagueId}&season={DateTimeOffset.UtcNow.Year}",
        FootballApiDataFactory.SuccessResponse(
            new FootballApiTeamsParameters(League: leagueId, Season: DateTimeOffset.UtcNow.Year),
            teams)
    );
}
```

- [ ] **Step 5: Build to verify no compilation errors**

```bash
dotnet build --configuration Release
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Common/Apis/FootballApi/ \
        tests/Personal.Dashboard.Core.Tests/Support/FootballApiDataFactory.cs \
        tests/Personal.Dashboard.Core.Tests/Support/FakeHttpMessageHandlerExtensions.cs
git commit -m "feat: add FootballApiTeam models and GetTeamsAsync"
```

---

### Task 5: Shared models + dashboard events + DataFactory update

**Files:**
- Modify: `src/Personal.Dashboard.Models/FootballModels.cs`
- Modify: `src/Personal.Dashboard.Models/DashboardEvents.cs`
- Modify: `tests/Personal.Dashboard.Test.Support/DataFactory.cs`

- [ ] **Step 1: Update FootballLeagueModel and add FootballClubModel**

Replace `src/Personal.Dashboard.Models/FootballModels.cs`:

```csharp
namespace Personal.Dashboard.Models;

public record FootballLeagueModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite = false
);

public record FootballClubModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite = false
);
```

- [ ] **Step 2: Add ClubsRefreshedDashboardEvent**

In `src/Personal.Dashboard.Models/DashboardEvents.cs`, add:

```csharp
public record ClubsRefreshedDashboardEvent() : DashboardEvent("ClubsRefreshed");
```

- [ ] **Step 3: Update DataFactory**

In `tests/Personal.Dashboard.Test.Support/DataFactory.cs`, update `FootballLeagueModel` and add `FootballClubModel`:

```csharp
public static FootballLeagueModel FootballLeagueModel()
{
    return new FootballLeagueModel(
        Faker.Random.Guid(),
        Faker.Company.CompanyName(),
        Faker.Date.RecentOffset(),
        Faker.Random.Bool()
    );
}

public static FootballClubModel FootballClubModel()
{
    return new FootballClubModel(
        Faker.Random.Guid(),
        Faker.Company.CompanyName(),
        Faker.Date.RecentOffset(),
        Faker.Random.Bool()
    );
}
```

- [ ] **Step 4: Build to verify no compilation errors**

```bash
dotnet build --configuration Release
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Models/ \
        tests/Personal.Dashboard.Test.Support/DataFactory.cs
git commit -m "feat: add FootballClubModel, IsFavorite to FootballLeagueModel, ClubsRefreshedDashboardEvent"
```

---

## Chunk 2: Core CQRS

### Task 6: FootballClubMapper + GetClubsQuery

**Files:**
- Create: `src/Personal.Dashboard.Core/Clubs/Mappers/FootballClubMapper.cs`
- Create: `src/Personal.Dashboard.Core/Clubs/Queries/GetClubsQuery.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Clubs/Queries/GetClubsQueryTests.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Queries/GetLeaguesQueryTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Clubs/Queries/GetClubsQueryTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Queries;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Queries;

public class GetClubsQueryTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public GetClubsQueryTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenNoClubsExistThenReturnsEmptyResult()
    {
        var result = await _bus.QueryAsync(new GetClubsQuery());
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task WhenClubsExistThenReturnsMappedClubs()
    {
        _context.AddMany(DataFactory.Many(() => PersonalDashboardEntityFactory.FootballClub(), 15));
        await _context.SaveChangesAsync();

        var result = await _bus.QueryAsync(new GetClubsQuery());
        Assert.Equal(15, result.Total);
        Assert.Equal(10, result.Items.Length);
    }
}
```

Add to `tests/Personal.Dashboard.Core.Tests/Leagues/Queries/GetLeaguesQueryTests.cs`:

```csharp
[Fact]
public async Task WhenLeagueIsFavoriteThenReturnedModelHasIsFavoriteTrue()
{
    var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
    _context.Add(league);
    await _context.SaveChangesAsync();

    var result = await _bus.QueryAsync(new GetLeaguesQuery());
    Assert.True(result.Items[0].IsFavorite);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "GetClubsQueryTests|GetLeaguesQueryTests"
```

Expected: FAIL

- [ ] **Step 3: Create FootballClubMapper**

Create `src/Personal.Dashboard.Core/Clubs/Mappers/FootballClubMapper.cs`:

```csharp
using AutoMapper;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Mappers;

public class FootballClubMapper : Profile
{
    public FootballClubMapper()
    {
        CreateMap<FootballClubEntity, FootballClubModel>();
    }
}
```

- [ ] **Step 4: Create GetClubsQuery**

Create `src/Personal.Dashboard.Core/Clubs/Queries/GetClubsQuery.cs`:

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
            .ProjectTo<FootballClubModel>(mapper.ConfigurationProvider);
        return await query.ToPagedListAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "GetClubsQueryTests|GetLeaguesQueryTests"
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/Mappers/ \
        src/Personal.Dashboard.Core/Clubs/Queries/ \
        tests/Personal.Dashboard.Core.Tests/Clubs/Queries/ \
        tests/Personal.Dashboard.Core.Tests/Leagues/Queries/GetLeaguesQueryTests.cs
git commit -m "feat: add GetClubsQuery and FootballClubMapper"
```

---

### Task 7: FavoriteClubCommand + UnfavoriteClubCommand

**Files:**
- Create: `src/Personal.Dashboard.Core/Clubs/Commands/FavoriteClubCommand.cs`
- Create: `src/Personal.Dashboard.Core/Clubs/Commands/UnfavoriteClubCommand.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/FavoriteClubCommandTests.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/UnfavoriteClubCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/FavoriteClubCommandTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class FavoriteClubCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public FavoriteClubCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenFavoriteClubCommandExecutedThenClubIsFavorite()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        _context.Add(club);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new FavoriteClubCommand(club.Id));

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenFavoriteCalledForNonExistentClubThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new FavoriteClubCommand(Guid.NewGuid())));
    }
}
```

Create `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/UnfavoriteClubCommandTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class UnfavoriteClubCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public UnfavoriteClubCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenUnfavoriteClubCommandExecutedThenClubIsNotFavorite()
    {
        var club = PersonalDashboardEntityFactory.FootballClub(c => c.Favorite());
        _context.Add(club);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new UnfavoriteClubCommand(club.Id));

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.False(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenUnfavoriteCalledForNonExistentClubThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new UnfavoriteClubCommand(Guid.NewGuid())));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteClubCommandTests|UnfavoriteClubCommandTests"
```

Expected: FAIL

- [ ] **Step 3: Implement FavoriteClubCommand**

Create `src/Personal.Dashboard.Core/Clubs/Commands/FavoriteClubCommand.cs`:

```csharp
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record FavoriteClubCommand(Guid ClubId) : ICommand;

public class FavoriteClubCommandHandler(PersonalDashboardContext context) : ICommandHandler<FavoriteClubCommand>
{
    public async Task Handle(FavoriteClubCommand request, CancellationToken cancellationToken)
    {
        var club = await context.Set<FootballClubEntity>().FindAsync([request.ClubId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballClubEntity), request.ClubId);
        club.Favorite();
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Implement UnfavoriteClubCommand**

Create `src/Personal.Dashboard.Core/Clubs/Commands/UnfavoriteClubCommand.cs`:

```csharp
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record UnfavoriteClubCommand(Guid ClubId) : ICommand;

public class UnfavoriteClubCommandHandler(PersonalDashboardContext context) : ICommandHandler<UnfavoriteClubCommand>
{
    public async Task Handle(UnfavoriteClubCommand request, CancellationToken cancellationToken)
    {
        var club = await context.Set<FootballClubEntity>().FindAsync([request.ClubId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballClubEntity), request.ClubId);
        club.Unfavorite();
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteClubCommandTests|UnfavoriteClubCommandTests"
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/Commands/FavoriteClubCommand.cs \
        src/Personal.Dashboard.Core/Clubs/Commands/UnfavoriteClubCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Clubs/Commands/
git commit -m "feat: add FavoriteClubCommand and UnfavoriteClubCommand"
```

---

### Task 8: RefreshClubsCommand + ClubsRefreshedEvent

**Files:**
- Create: `src/Personal.Dashboard.Core/Clubs/Events/ClubsRefreshedEvent.cs`
- Create: `src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class RefreshClubsCommandTests
{
    private const string BaseUrl = "https://football.api.com";
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
    public async Task WhenRefreshingClubsForLeagueThenCreatesNewClubInDatabase()
    {
        var (league, _) = await SeedLeagueAsync(leagueApiAlias: "10");
        var team = FootballApiDataFactory.Team();
        await _handler.SetupGetTeams(BaseUrl, 10, [team]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id));

        var clubs = await _context.Set<FootballClubEntity>().ToArrayAsync();
        Assert.Single(clubs);
        Assert.Equal(team.Team.Name, clubs[0].Name);
    }

    [Fact]
    public async Task WhenRefreshingClubsForLeagueThenUpdatesExistingClubInDatabase()
    {
        var (league, club) = await SeedLeagueWithClubAsync(leagueApiAlias: "10", clubApiAlias: "99");
        var team = FootballApiDataFactory.Team();
        var teamWithKnownId = team with { Team = team.Team with { Id = 99 } };
        await _handler.SetupGetTeams(BaseUrl, 10, [teamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id));

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.Equal(teamWithKnownId.Team.Name, updated?.Name);
        Assert.NotNull(updated?.LastRefreshed);
    }

    [Fact]
    public async Task WhenRefreshingClubsForLeagueThenPublishesClubsRefreshedEvent()
    {
        var (league, _) = await SeedLeagueAsync(leagueApiAlias: "10");
        await _handler.SetupGetTeams(BaseUrl, 10, [FootballApiDataFactory.Team()]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand(league.Id));

        Assert.Single(_cqrsBus.GetCapturedEvents<ClubsRefreshedEvent>());
    }

    [Fact]
    public async Task WhenRefreshingAllClubsWithNoExistingClubsThenIsNoOp()
    {
        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        var clubs = await _context.Set<FootballClubEntity>().ToArrayAsync();
        Assert.Empty(clubs);
    }

    [Fact]
    public async Task WhenRefreshingAllClubsThenUpdatesExistingClubs()
    {
        var (_, club) = await SeedLeagueWithClubAsync(leagueApiAlias: "10", clubApiAlias: "99");
        var team = FootballApiDataFactory.Team();
        var teamWithKnownId = team with { Team = team.Team with { Id = 99 } };
        await _handler.SetupGetTeams(BaseUrl, 10, [teamWithKnownId]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.Equal(teamWithKnownId.Team.Name, updated?.Name);
    }

    [Fact]
    public async Task WhenRefreshingAllClubsThenPublishesClubsRefreshedEvent()
    {
        var (_, club) = await SeedLeagueWithClubAsync(leagueApiAlias: "10", clubApiAlias: "99");
        var team = FootballApiDataFactory.Team() with { Team = FootballApiDataFactory.TeamInfo() with { Id = 99 } };
        await _handler.SetupGetTeams(BaseUrl, 10, [team]);

        await _cqrsBus.ExecuteAsync(new RefreshClubsCommand());

        Assert.Single(_cqrsBus.GetCapturedEvents<ClubsRefreshedEvent>());
    }

    private async Task<(Personal.Dashboard.Core.Leagues.Entities.FootballLeagueEntity,
        FootballClubEntity)> SeedLeagueWithClubAsync(string leagueApiAlias, string clubApiAlias)
    {
        var (league, club) = PersonalDashboardEntityFactory.FootballClubInLeague(leagueApiAlias, clubApiAlias);
        _context.Add(league);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return (league, club);
    }

    private async Task<(Personal.Dashboard.Core.Leagues.Entities.FootballLeagueEntity,
        FootballClubEntity)> SeedLeagueAsync(string leagueApiAlias)
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        _context.Add(league);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return (league, new FootballClubEntity());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshClubsCommandTests"
```

Expected: FAIL

- [ ] **Step 3: Create ClubsRefreshedEvent**

Create `src/Personal.Dashboard.Core/Clubs/Events/ClubsRefreshedEvent.cs`:

```csharp
using Personal.Dashboard.Core.Common.Cqrs.Events;

namespace Personal.Dashboard.Core.Clubs.Events;

public record ClubsRefreshedEvent : IEvent;
```

- [ ] **Step 4: Implement RefreshClubsCommand**

Create `src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record RefreshClubsCommand(Guid? LeagueId = null) : ICommand;

public class RefreshClubsCommandHandler(
    IFootballApiClient client,
    PersonalDashboardContext context,
    ICqrsBus bus
) : ICommandHandler<RefreshClubsCommand>
{
    public async Task Handle(RefreshClubsCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            await RefreshForLeagueAsync(request.LeagueId.Value, cancellationToken);
        else
            await RefreshAllExistingClubsAsync(cancellationToken);
    }

    private async Task RefreshForLeagueAsync(Guid leagueId, CancellationToken ct)
    {
        var league = await context.Set<FootballLeagueEntity>()
            .Include(l => l.Aliases)
            .FirstAsync(l => l.Id == leagueId, ct);

        var leagueApiAlias = league.Aliases.First(a => a.AliasSource == DataSource.FootballApi);

        var existingAliases = await context.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi &&
                        a.Club.Leagues.Any(l => l.Id == leagueId))
            .Include(a => a.Club)
            .ToDictionaryAsync(a => a.Alias, ct);

        var response = await client.GetTeamsAsync(new FootballApiTeamsParameters(
            League: long.Parse(leagueApiAlias.Alias),
            Season: DateTimeOffset.UtcNow.Year));

        ProcessTeams(response.Response, league, existingAliases);

        await context.SaveChangesAsync(ct);
        await bus.PublishAsync(new ClubsRefreshedEvent(), ct);
    }

    private async Task RefreshAllExistingClubsAsync(CancellationToken ct)
    {
        var allClubAliases = await context.Set<FootballClubAlias>()
            .Where(a => a.AliasSource == DataSource.FootballApi)
            .Include(a => a.Club).ThenInclude(c => c.Leagues).ThenInclude(l => l.Aliases)
            .ToListAsync(ct);

        if (!allClubAliases.Any()) return;

        var leagueGroups = allClubAliases
            .SelectMany(a => a.Club.Leagues.Select(l => (League: l, ClubAlias: a)))
            .GroupBy(x => x.League.Id)
            .ToList();

        foreach (var group in leagueGroups)
        {
            var league = group.First().League;
            var leagueApiAlias = league.Aliases.FirstOrDefault(a => a.AliasSource == DataSource.FootballApi);
            if (leagueApiAlias is null) continue;

            var aliasDict = group.ToDictionary(x => x.ClubAlias.Alias, x => x.ClubAlias);

            var response = await client.GetTeamsAsync(new FootballApiTeamsParameters(
                League: long.Parse(leagueApiAlias.Alias),
                Season: DateTimeOffset.UtcNow.Year));

            ProcessTeams(response.Response, league, aliasDict);
        }

        await context.SaveChangesAsync(ct);
        await bus.PublishAsync(new ClubsRefreshedEvent(), ct);
    }

    private void ProcessTeams(
        FootballApiTeam[] teams,
        FootballLeagueEntity league,
        Dictionary<string, FootballClubAlias> existingAliases)
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
                context.Add(club);
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "RefreshClubsCommandTests"
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Core/Clubs/Commands/RefreshClubsCommand.cs \
        src/Personal.Dashboard.Core/Clubs/Events/ \
        tests/Personal.Dashboard.Core.Tests/Clubs/Commands/RefreshClubsCommandTests.cs
git commit -m "feat: add RefreshClubsCommand with shared ProcessTeams and ClubsRefreshedEvent"
```

---

### Task 9: FavoriteLeagueCommand + UnfavoriteLeagueCommand

**Files:**
- Create: `src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs`
- Create: `src/Personal.Dashboard.Core/Leagues/Commands/UnfavoriteLeagueCommand.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs`
- Create: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/UnfavoriteLeagueCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class FavoriteLeagueCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly CapturingCqrsBus _cqrsBus;

    public FavoriteLeagueCommandTests()
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
    public async Task WhenFavoriteLeagueCommandExecutedThenLeagueIsFavorite()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, "10"));
        _context.Add(league);
        await _context.SaveChangesAsync();
        await _handler.SetupGetTeams(BaseUrl, 10, [FootballApiDataFactory.Team()]);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenFavoriteLeagueCommandExecutedThenDispatchesRefreshClubsCommand()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.AddAlias(DataSource.FootballApi, "10"));
        _context.Add(league);
        await _context.SaveChangesAsync();
        await _handler.SetupGetTeams(BaseUrl, 10, [FootballApiDataFactory.Team()]);

        await _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(league.Id));

        var dispatched = _cqrsBus.GetCapturedCommands<RefreshClubsCommand>().ToList();
        Assert.Single(dispatched);
        Assert.Equal(league.Id, dispatched[0].LeagueId);
    }

    [Fact]
    public async Task WhenFavoriteCalledForNonExistentLeagueThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _cqrsBus.ExecuteAsync(new FavoriteLeagueCommand(Guid.NewGuid())));
    }
}
```

Create `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/UnfavoriteLeagueCommandTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class UnfavoriteLeagueCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public UnfavoriteLeagueCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenUnfavoriteLeagueCommandExecutedThenLeagueIsNotFavorite()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        _context.Add(league);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new UnfavoriteLeagueCommand(league.Id));

        var updated = await _context.Set<FootballLeagueEntity>().FindAsync(league.Id);
        Assert.False(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenUnfavoriteCalledForNonExistentLeagueThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new UnfavoriteLeagueCommand(Guid.NewGuid())));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteLeagueCommandTests|UnfavoriteLeagueCommandTests"
```

Expected: FAIL

- [ ] **Step 3: Implement FavoriteLeagueCommand**

Create `src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs`:

```csharp
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record FavoriteLeagueCommand(Guid LeagueId) : ICommand;

public class FavoriteLeagueCommandHandler(
    PersonalDashboardContext context,
    ICqrsBus bus
) : ICommandHandler<FavoriteLeagueCommand>
{
    public async Task Handle(FavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await context.Set<FootballLeagueEntity>().FindAsync([request.LeagueId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);
        league.Favorite();
        await context.SaveChangesAsync(cancellationToken);
        await bus.ExecuteAsync(new RefreshClubsCommand(request.LeagueId));
    }
}
```

- [ ] **Step 4: Implement UnfavoriteLeagueCommand**

Create `src/Personal.Dashboard.Core/Leagues/Commands/UnfavoriteLeagueCommand.cs`:

```csharp
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Leagues.Commands;

public record UnfavoriteLeagueCommand(Guid LeagueId) : ICommand;

public class UnfavoriteLeagueCommandHandler(PersonalDashboardContext context) : ICommandHandler<UnfavoriteLeagueCommand>
{
    public async Task Handle(UnfavoriteLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await context.Set<FootballLeagueEntity>().FindAsync([request.LeagueId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballLeagueEntity), request.LeagueId);
        league.Unfavorite();
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "FavoriteLeagueCommandTests|UnfavoriteLeagueCommandTests"
```

Expected: PASS

- [ ] **Step 6: Now run the previously-written CqrsControllerNotFoundTests**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release --filter "CqrsControllerNotFoundTests"
```

Expected: PASS — `FavoriteLeagueCommand` now throws `EntityNotFoundException` for a missing league, and `CqrsController` returns 404.

- [ ] **Step 7: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Commands/FavoriteLeagueCommand.cs \
        src/Personal.Dashboard.Core/Leagues/Commands/UnfavoriteLeagueCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/FavoriteLeagueCommandTests.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/UnfavoriteLeagueCommandTests.cs
git commit -m "feat: add FavoriteLeagueCommand and UnfavoriteLeagueCommand"
```

---

### Task 10: Update RefreshLeaguesCommand to dispatch RefreshClubsCommand

**Files:**
- Modify: `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`
- Modify: `tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs`

- [ ] **Step 1: Write a failing test**

Add to `RefreshLeaguesCommandTests.cs`:

```csharp
[Fact]
public async Task WhenRefreshingLeaguesThenDispatchesRefreshClubsCommand()
{
    await _handler.SetupGetLeagues(BaseUrl, [FootballApiDataFactory.League()]);

    await _cqrsBus.ExecuteAsync(new RefreshLeaguesCommand());

    Assert.Single(_cqrsBus.GetCapturedCommands<RefreshClubsCommand>());
}
```

Note: `RefreshLeaguesCommandTests` already uses `CapturingCqrsBus`. Add `using Personal.Dashboard.Core.Clubs.Commands;` at the top of the test file.

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release --filter "WhenRefreshingLeaguesThenDispatchesRefreshClubsCommand"
```

Expected: FAIL

- [ ] **Step 3: Update RefreshLeaguesCommand handler**

In `src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs`, add dispatch after `PublishAsync`:

```csharp
await context.SaveChangesAsync(cancellationToken);
await bus.PublishAsync(new LeaguesRefreshedEvent(), cancellationToken);
await bus.ExecuteAsync(new RefreshClubsCommand());
```

Add `using Personal.Dashboard.Core.Clubs.Commands;` at the top.

- [ ] **Step 4: Run all core tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Core.Tests --configuration Release
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Core/Leagues/Commands/RefreshLeaguesCommand.cs \
        tests/Personal.Dashboard.Core.Tests/Leagues/Commands/RefreshLeaguesCommandTests.cs
git commit -m "feat: RefreshLeaguesCommand dispatches RefreshClubsCommand after refreshing"
```

---

## Chunk 3: API Layer + Web UI

### Task 11: ClubsController + ClubsRefreshedEventHandler

**Files:**
- Create: `src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs`
- Create: `src/Personal.Dashboard.Api.Host/Clubs/ClubsRefreshedEventHandler.cs`
- Create: `tests/Personal.Dashboard.Api.Host.Tests/Clubs/ClubsApiTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Api.Host.Tests/Clubs/ClubsApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Api.Host.Tests.Clubs;

public class ClubsApiTests(PersonalDashboardApiApplication app) : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenGettingClubsThenReturnsClubsFromDatabase()
    {
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballClub());
        await app.AddToDbAsync(PersonalDashboardEntityFactory.FootballClub());

        var response = await _client.GetAsync("/clubs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedListResultModel<FootballClubModel>>();
        Assert.True(result?.Total >= 2);
    }

    [Fact]
    public async Task WhenRefreshingClubsThenReturnsNoContent()
    {
        var response = await _client.PostAsync("/clubs/refresh", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoringClubThenReturnsNoContent()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        await app.AddToDbAsync(club);

        var response = await _client.PostAsync($"/clubs/{club.Id}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnfavoringClubThenReturnsNoContent()
    {
        var club = PersonalDashboardEntityFactory.FootballClub(c => c.Favorite());
        await app.AddToDbAsync(club);

        var response = await _client.PostAsync($"/clubs/{club.Id}/unfavorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoringNonExistentClubThenReturnsNotFound()
    {
        var response = await _client.PostAsync($"/clubs/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WhenRefreshingClubsThenSendsClubsRefreshedEventViaSignalR()
    {
        DashboardEvent? receivedEvent = null;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(app.Server.BaseAddress, "hubs/events"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();

        connection.On<DashboardEvent>("ReceiveEvent", e => receivedEvent = e);
        await connection.StartAsync();

        await _client.PostAsync("/clubs/refresh", null);

        await Eventually.Assert(() =>
        {
            Assert.NotNull(receivedEvent);
            Assert.Equal("ClubsRefreshed", receivedEvent.Type);
        });

        await connection.StopAsync();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release --filter "ClubsApiTests"
```

Expected: FAIL — routes not found.

- [ ] **Step 3: Implement ClubsController**

Create `src/Personal.Dashboard.Api.Host/Clubs/ClubsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Api.Host.Common;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Queries;
using Personal.Dashboard.Core.Common.Cqrs;

namespace Personal.Dashboard.Api.Host.Clubs;

[ApiController]
[Route("[controller]")]
public class ClubsController(ICqrsBus cqrsBus) : CqrsController(cqrsBus)
{
    [HttpGet]
    public async Task<IActionResult> GetClubs([FromQuery] int offset = 0, [FromQuery] int limit = 10)
    {
        return await QueryAsync(new GetClubsQuery(offset, limit));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshClubs()
    {
        return await ExecuteAsync(new RefreshClubsCommand(), statusCode: 204);
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<IActionResult> FavoriteClub(Guid id)
    {
        return await ExecuteAsync(new FavoriteClubCommand(id), statusCode: 204);
    }

    [HttpPost("{id:guid}/unfavorite")]
    public async Task<IActionResult> UnfavoriteClub(Guid id)
    {
        return await ExecuteAsync(new UnfavoriteClubCommand(id), statusCode: 204);
    }
}
```

- [ ] **Step 4: Implement ClubsRefreshedEventHandler**

Create `src/Personal.Dashboard.Api.Host/Clubs/ClubsRefreshedEventHandler.cs`:

```csharp
using Personal.Dashboard.Api.Host.Common.SignalR;
using Personal.Dashboard.Core.Clubs.Events;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Api.Host.Clubs;

public class ClubsRefreshedEventHandler(ISignalRPublisher publisher) : IEventHandler<ClubsRefreshedEvent>
{
    public async Task Handle(ClubsRefreshedEvent notification, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(new ClubsRefreshedDashboardEvent(), cancellationToken)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release --filter "ClubsApiTests"
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/Personal.Dashboard.Api.Host/Clubs/ \
        tests/Personal.Dashboard.Api.Host.Tests/Clubs/
git commit -m "feat: add ClubsController and ClubsRefreshedEventHandler"
```

---

### Task 12: Leagues favorite/unfavorite endpoints

**Files:**
- Modify: `src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs`
- Create: `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesFavoriteApiTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesFavoriteApiTests.cs`:

```csharp
using System.Net;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesFavoriteApiTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenFavoringLeagueThenReturnsNoContent()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(
            l => l.AddAlias(DataSource.FootballApi, "10"));
        await app.AddToDbAsync(league);
        await app.HttpHandler.SetupGetTeams(
            PersonalDashboardApiApplication.FootballApiBaseUrl, 10, [FootballApiDataFactory.Team()]);

        var response = await _client.PostAsync($"/leagues/{league.Id}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnfavoringLeagueThenReturnsNoContent()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        await app.AddToDbAsync(league);

        var response = await _client.PostAsync($"/leagues/{league.Id}/unfavorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhenFavoringNonExistentLeagueThenReturnsNotFound()
    {
        var response = await _client.PostAsync($"/leagues/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release --filter "LeaguesFavoriteApiTests"
```

Expected: FAIL — routes not found.

- [ ] **Step 3: Add endpoints to LeaguesController**

In `src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs`, add:

```csharp
[HttpPost("{id:guid}/favorite")]
public async Task<IActionResult> FavoriteLeague(Guid id)
{
    return await ExecuteAsync(new FavoriteLeagueCommand(id), statusCode: 204);
}

[HttpPost("{id:guid}/unfavorite")]
public async Task<IActionResult> UnfavoriteLeague(Guid id)
{
    return await ExecuteAsync(new UnfavoriteLeagueCommand(id), statusCode: 204);
}
```

Add `using Personal.Dashboard.Core.Leagues.Commands;` if not already present (it covers both new commands).

- [ ] **Step 4: Run all API tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Api.Host.Tests --configuration Release
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Personal.Dashboard.Api.Host/Leagues/LeaguesController.cs \
        tests/Personal.Dashboard.Api.Host.Tests/Leagues/LeaguesFavoriteApiTests.cs
git commit -m "feat: add favorite/unfavorite endpoints to LeaguesController"
```

---

### Task 13: API client + LeaguesList favorite toggle

**Files:**
- Modify: `src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs`
- Modify: `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`
- Create: `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardClubsApiExtensions.cs`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs`
- Modify: `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`

- [ ] **Step 1: Add API client methods**

In `src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs`, add after `GetLeaguesAsync`:

```csharp
public async Task<PagedListResultModel<FootballClubModel>> GetClubsAsync(
    PagedListParameters? parameters = null)
{
    var queryParameters = parameters ?? PagedListParameters.Default();
    return await GetJsonAsync<PagedListResultModel<FootballClubModel>>(
        $"/clubs?{queryParameters.ToQueryString()}"
    ).ConfigureAwait(false);
}

public async Task RefreshClubsAsync(CancellationToken ct = default)
{
    var response = await client.PostAsync("/clubs/refresh", null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}

public async Task FavoriteClubAsync(Guid id, CancellationToken ct = default)
{
    var response = await client.PostAsync($"/clubs/{id}/favorite", null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}

public async Task UnfavoriteClubAsync(Guid id, CancellationToken ct = default)
{
    var response = await client.PostAsync($"/clubs/{id}/unfavorite", null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}

public async Task FavoriteLeagueAsync(Guid id, CancellationToken ct = default)
{
    var response = await client.PostAsync($"/leagues/{id}/favorite", null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}

public async Task UnfavoriteLeagueAsync(Guid id, CancellationToken ct = default)
{
    var response = await client.PostAsync($"/leagues/{id}/unfavorite", null, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}
```

- [ ] **Step 2: Add favorite/unfavorite helpers to leagues test extensions**

In `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardLeaguesApiExtensions.cs`, add:

```csharp
public static async Task SetupFavoriteLeague(
    this FakeHttpMessageHandler handler,
    Guid leagueId,
    ConfigureResponseOptions? options = null)
{
    await handler.SetupResponseAsync(
        new HttpRequestMessage(HttpMethod.Post, $"http://api/leagues/{leagueId}/favorite"),
        new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
        options
    );
}

public static async Task SetupUnfavoriteLeague(
    this FakeHttpMessageHandler handler,
    Guid leagueId,
    ConfigureResponseOptions? options = null)
{
    await handler.SetupResponseAsync(
        new HttpRequestMessage(HttpMethod.Post, $"http://api/leagues/{leagueId}/unfavorite"),
        new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
        options
    );
}
```

- [ ] **Step 3: Create club API extensions**

Create `tests/Personal.Dashboard.Web.Host.Tests/Support/PersonalDashboardClubsApiExtensions.cs`:

```csharp
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public static class PersonalDashboardClubsApiExtensions
{
    public static async Task SetupClubs(
        this FakeHttpMessageHandler handler,
        long offset = 0,
        long limit = 10,
        long total = 10,
        ConfigureResponseOptions? options = null,
        params FootballClubModel[] clubs)
    {
        var result = DataFactory.PagedListModel(total, offset, limit, clubs);
        await handler.SetupGetJsonResponseAsync("http://api/clubs", result, options);
    }

    public static async Task SetupRefreshClubs(
        this FakeHttpMessageHandler handler,
        ConfigureResponseOptions? options = null)
    {
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://api/clubs/refresh"),
            new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
            options
        );
    }

    public static async Task SetupFavoriteClub(
        this FakeHttpMessageHandler handler,
        Guid clubId,
        ConfigureResponseOptions? options = null)
    {
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/clubs/{clubId}/favorite"),
            new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
            options
        );
    }

    public static async Task SetupUnfavoriteClub(
        this FakeHttpMessageHandler handler,
        Guid clubId,
        ConfigureResponseOptions? options = null)
    {
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/clubs/{clubId}/unfavorite"),
            new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
            options
        );
    }
}
```

- [ ] **Step 4: Write failing tests for LeaguesList favorite toggle**

Add to `tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs`:

```csharp
[Fact]
public async Task WhenFavoriteToggledOnThenCallsFavoriteEndpoint()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
    await context.HttpHandler.SetupLeagues(leagues: [league]);
    HttpRequestMessage? favoriteRequest = null;
    await context.HttpHandler.SetupFavoriteLeague(league.Id,
        new ConfigureResponseOptions(Capture: req => favoriteRequest = req));
    await context.HttpHandler.SetupLeagues(leagues: [league with { IsFavorite = true }]);

    var page = context.Render<LeaguesList>();
    await Eventually.Assert(() =>
        Assert.NotNull(page.FindByRole("button", new FindByRoleOptions(Label: "favorite"))));
    await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

    await Eventually.Assert(() => Assert.NotNull(favoriteRequest));
}

[Fact]
public async Task WhenFavoriteToggledOffThenCallsUnfavoriteEndpoint()
{
    await using var context = new PersonalDashboardWebContext();
    var league = DataFactory.FootballLeagueModel() with { IsFavorite = true };
    await context.HttpHandler.SetupLeagues(leagues: [league]);
    HttpRequestMessage? unfavoriteRequest = null;
    await context.HttpHandler.SetupUnfavoriteLeague(league.Id,
        new ConfigureResponseOptions(Capture: req => unfavoriteRequest = req));
    await context.HttpHandler.SetupLeagues(leagues: [league with { IsFavorite = false }]);

    var page = context.Render<LeaguesList>();
    await Eventually.Assert(() =>
        Assert.NotNull(page.FindByRole("button", new FindByRoleOptions(Label: "unfavorite"))));
    await page.FindByRole("button", new FindByRoleOptions(Label: "unfavorite")).ClickAsync();

    await Eventually.Assert(() => Assert.NotNull(unfavoriteRequest));
}
```

- [ ] **Step 5: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeaguesListTests"
```

Expected: FAIL on the two new favorite toggle tests.

- [ ] **Step 6: Update LeaguesList.razor with favorite toggle**

In `src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor`, update the list item inside the foreach and add the toggle handler. Updated foreach:

```razor
@foreach (var league in Leagues.Items)
{
    <MudListItem>
        <MudStack Row="true" AlignItems="AlignItems.Center">
            <MudStack>
                <MudText Typo="Typo.body1">@league.Name</MudText>
                @if (league.LastRefreshed.HasValue)
                {
                    <MudText Typo="Typo.caption">@league.LastRefreshed.Value.ToString("g")</MudText>
                }
            </MudStack>
            <MudIconButton Icon="@(league.IsFavorite ? Icons.Material.Rounded.Star : Icons.Material.Rounded.StarBorder)"
                           OnClick="@(() => OnFavoriteToggled(league))"
                           role="button"
                           aria-label="@(league.IsFavorite ? "unfavorite" : "favorite")" />
        </MudStack>
    </MudListItem>
}
```

Add to `@code`:

```csharp
[Inject] ISnackbar Snackbar { get; set; } = default!;

private async Task OnFavoriteToggled(FootballLeagueModel league)
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
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "LeaguesListTests"
```

Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Common/Apis/PersonalDashboardApiClient.cs \
        src/Personal.Dashboard.Web.Host/Leagues/LeaguesList.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Support/ \
        tests/Personal.Dashboard.Web.Host.Tests/Leagues/LeaguesListTests.cs
git commit -m "feat: add favorite toggle to LeaguesList and API client methods"
```

---

### Task 14: ClubsList + Clubs page + nav

**Files:**
- Create: `src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor`
- Create: `src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor`
- Modify: `src/Personal.Dashboard.Web.Host/Layout/NavigationMenu.razor`
- Create: `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Personal.Dashboard.Web.Host.Tests/Clubs/ClubsListTests.cs`:

```csharp
using AngleSharp.Dom;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Web.Host.Clubs;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Clubs;

public class ClubsListTests
{
    [Fact]
    public async Task WhenNoClubsExistThenDisplaysEmptyStateMessage()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupClubs(total: 0, clubs: []);

        var page = context.Render<ClubsList>();

        await Eventually.Assert(() =>
            Assert.Contains("clubs appear after favoriting a league", page.Markup));
    }

    [Fact]
    public async Task WhenClubsExistThenDisplaysClubNames()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel();
        await context.HttpHandler.SetupClubs(clubs: [club]);

        var page = context.Render<ClubsList>();

        await Eventually.Assert(() =>
            Assert.Contains(page.FindAll(".mud-list-item"),
                item => item.TextContent.Contains(club.Name)));
    }

    [Fact]
    public async Task WhenClubHasLastRefreshedThenDisplaysIt()
    {
        await using var context = new PersonalDashboardWebContext();
        var lastRefreshed = DateTimeOffset.UtcNow.AddHours(-1);
        var club = DataFactory.FootballClubModel() with { LastRefreshed = lastRefreshed };
        await context.HttpHandler.SetupClubs(clubs: [club]);

        var page = context.Render<ClubsList>();

        await Eventually.Assert(() =>
            Assert.Contains(lastRefreshed.ToString("g"), page.Markup));
    }

    [Fact]
    public async Task WhenRefreshButtonClickedThenCallsRefreshEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? refreshRequest = null;
        await context.HttpHandler.SetupClubs();
        await context.HttpHandler.SetupRefreshClubs(
            new ConfigureResponseOptions(Capture: req => refreshRequest = req));

        var page = context.Render<ClubsList>();
        await page.FindByRole("button", new FindByRoleOptions(Label: "refresh")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(refreshRequest));
    }

    [Fact]
    public async Task WhenTotalIsLessThanLimitThenDisablesNext()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupClubs();

        var page = context.Render<ClubsList>();
        var nextButton = page.FindByRole("button", new FindByRoleOptions(Label: "next"));

        Assert.True(nextButton.IsDisabled());
    }

    [Fact]
    public async Task WhenClubsRefreshedEventReceivedThenRefetchesClubs()
    {
        await using var context = new PersonalDashboardWebContext();
        var refreshedClub = DataFactory.FootballClubModel();
        await context.HttpHandler.SetupClubs(clubs: [refreshedClub]);

        var page = context.Render<ClubsList>();
        await context.HubFactory.Connection.SimulateEventAsync(new ClubsRefreshedDashboardEvent());

        await Eventually.Assert(() =>
            Assert.Contains(page.FindAll(".mud-list-item"),
                item => item.TextContent.Contains(refreshedClub.Name)));
    }

    [Fact]
    public async Task WhenFavoriteToggledOnThenCallsFavoriteEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        await context.HttpHandler.SetupClubs(clubs: [club]);
        HttpRequestMessage? favoriteRequest = null;
        await context.HttpHandler.SetupFavoriteClub(club.Id,
            new ConfigureResponseOptions(Capture: req => favoriteRequest = req));
        await context.HttpHandler.SetupClubs(clubs: [club with { IsFavorite = true }]);

        var page = context.Render<ClubsList>();
        await Eventually.Assert(() =>
            Assert.NotNull(page.FindByRole("button", new FindByRoleOptions(Label: "favorite"))));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(favoriteRequest));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release --filter "ClubsListTests"
```

Expected: FAIL — `ClubsList` component not found.

- [ ] **Step 3: Create ClubsList.razor**

Create `src/Personal.Dashboard.Web.Host/Clubs/ClubsList.razor`:

```razor
@using Personal.Dashboard.Models
@using Personal.Dashboard.Web.Host.Common.Apis

@inject PersonalDashboardApiClient Client
@inject ISnackbar Snackbar
@implements IAsyncDisposable

<MudGrid>
    <MudItem xs="12">
        <MudStack Row="true" AlignItems="AlignItems.Center">
            <MudText Typo="Typo.h6">Clubs</MudText>
            <MudIconButton Icon="@Icons.Material.Rounded.Refresh"
                           OnClick="@OnRefreshClicked"
                           role="button"
                           aria-label="refresh" />
        </MudStack>
    </MudItem>
    <MudItem xs="12">
        <MudPaper Class="d-flex flex-1">
            <MudList Class="d-flex flex-1" T="FootballClubModel">
                @if (!Clubs.Items.Any())
                {
                    <MudListItem>
                        <MudText>No clubs yet — clubs appear after favoriting a league</MudText>
                    </MudListItem>
                }
                @foreach (var club in Clubs.Items)
                {
                    <MudListItem>
                        <MudStack Row="true" AlignItems="AlignItems.Center">
                            <MudStack>
                                <MudText Typo="Typo.body1">@club.Name</MudText>
                                @if (club.LastRefreshed.HasValue)
                                {
                                    <MudText Typo="Typo.caption">@club.LastRefreshed.Value.ToString("g")</MudText>
                                }
                            </MudStack>
                            <MudIconButton Icon="@(club.IsFavorite ? Icons.Material.Rounded.Star : Icons.Material.Rounded.StarBorder)"
                                           OnClick="@(() => OnFavoriteToggled(club))"
                                           role="button"
                                           aria-label="@(club.IsFavorite ? "unfavorite" : "favorite")" />
                        </MudStack>
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
    private PagedListResultModel<FootballClubModel> Clubs { get; set; } =
        PagedListResultModel<FootballClubModel>.Empty();

    private bool HasNextPage => Clubs.Total > Clubs.Limit;
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

    private async Task LoadClubsAsync()
    {
        Clubs = await Client.GetClubsAsync(_currentParameters);
    }

    private async Task OnRefreshClicked()
    {
        await Client.RefreshClubsAsync();
    }

    private async Task OnFavoriteToggled(FootballClubModel club)
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

- [ ] **Step 4: Create Clubs.razor**

Create `src/Personal.Dashboard.Web.Host/Clubs/Clubs.razor`:

```razor
@page "/clubs"

<MudGrid>
    <MudItem xs="3">
        <ClubsList />
    </MudItem>
    <MudItem xs="9">
    </MudItem>
</MudGrid>
```

- [ ] **Step 5: Add Clubs to navigation**

In `src/Personal.Dashboard.Web.Host/Layout/NavigationMenu.razor`, add after the Leagues nav link:

```razor
<MudNavLink Href="/clubs" Match="NavLinkMatch.All">
    Clubs
</MudNavLink>
```

- [ ] **Step 6: Run all web tests to verify they pass**

```bash
dotnet test tests/Personal.Dashboard.Web.Host.Tests --configuration Release
```

Expected: PASS

- [ ] **Step 7: Run all tests**

```bash
dotnet test --configuration Release
```

Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/Personal.Dashboard.Web.Host/Clubs/ \
        src/Personal.Dashboard.Web.Host/Layout/NavigationMenu.razor \
        tests/Personal.Dashboard.Web.Host.Tests/Clubs/
git commit -m "feat: add ClubsList, Clubs page, and Clubs nav entry"
```

---

## Chunk 4: Feature Tests (Playwright + Aspire)

### Task 15: Create Personal.Dashboard.Feature.Tests project + fixtures

**Files:**
- Create: `tests/Personal.Dashboard.Feature.Tests/Personal.Dashboard.Feature.Tests.csproj`
- Create: `tests/Personal.Dashboard.Feature.Tests/Fixtures/ApplicationFixture.cs`
- Create: `tests/Personal.Dashboard.Feature.Tests/Fixtures/PlaywrightFixture.cs`

- [ ] **Step 1: Create project via dotnet new**

```bash
dotnet new xunit -o tests/Personal.Dashboard.Feature.Tests
```

- [ ] **Step 2: Replace csproj with minimal content**

Replace `tests/Personal.Dashboard.Feature.Tests/Personal.Dashboard.Feature.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Testing" Version="13.1.1" />
    <PackageReference Include="Microsoft.Playwright" Version="1.50.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Personal.Dashboard.Host\Personal.Dashboard.Host.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Delete the generated test file**

```bash
rm tests/Personal.Dashboard.Feature.Tests/UnitTest1.cs
```

- [ ] **Step 4: Add project to solution**

```bash
dotnet sln add tests/Personal.Dashboard.Feature.Tests/Personal.Dashboard.Feature.Tests.csproj
```

- [ ] **Step 5: Create ApplicationFixture**

Create `tests/Personal.Dashboard.Feature.Tests/Fixtures/ApplicationFixture.cs`:

```csharp
using Aspire.Hosting.Testing;

namespace Personal.Dashboard.Feature.Tests.Fixtures;

public class ApplicationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Personal_Dashboard_Host>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
    }

    public Uri GetEndpoint(string resourceName) => _app?.GetEndpoint(resourceName)
        ?? throw new InvalidOperationException("Application not started.");

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}

[CollectionDefinition("Application")]
public class ApplicationCollection : ICollectionFixture<ApplicationFixture>;
```

- [ ] **Step 6: Create PlaywrightFixture**

Create `tests/Personal.Dashboard.Feature.Tests/Fixtures/PlaywrightFixture.cs`:

```csharp
using Microsoft.Playwright;

namespace Personal.Dashboard.Feature.Tests.Fixtures;

public class PlaywrightFixture(ApplicationFixture appFixture) : IAsyncLifetime
{
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        var playwright = await Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> GoToPageAsync(string path)
    {
        var page = await (_browser ?? throw new InvalidOperationException("Browser not started."))
            .NewPageAsync();
        await page.GotoAsync($"{appFixture.GetEndpoint("webfrontend")}{path}");
        return page;
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
    }
}
```

- [ ] **Step 7: Install Playwright browsers**

```bash
dotnet build tests/Personal.Dashboard.Feature.Tests --configuration Release
pwsh tests/Personal.Dashboard.Feature.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

- [ ] **Step 8: Commit**

```bash
git add tests/Personal.Dashboard.Feature.Tests/ \
        *.sln
git commit -m "feat: add Feature.Tests project with ApplicationFixture and PlaywrightFixture"
```

---

### Task 16: Playwright feature tests

**Files:**
- Create: `tests/Personal.Dashboard.Feature.Tests/Leagues/LeagueFavoritingTests.cs`
- Create: `tests/Personal.Dashboard.Feature.Tests/Clubs/ClubFavoritingTests.cs`

- [ ] **Step 1: Create LeagueFavoritingTests**

Create `tests/Personal.Dashboard.Feature.Tests/Leagues/LeagueFavoritingTests.cs`:

```csharp
using Personal.Dashboard.Feature.Tests.Fixtures;

namespace Personal.Dashboard.Feature.Tests.Leagues;

[Collection("Application")]
public class LeagueFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task WhenViewingLeaguesPageThenLeaguesAreDisplayed()
    {
        var page = await playwright.GoToPageAsync("/leagues");
        await page.WaitForSelectorAsync(".mud-list-item");
        var items = await page.QuerySelectorAllAsync(".mud-list-item");
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task WhenFavoriteButtonClickedThenLeagueIsMarkedFavorite()
    {
        var page = await playwright.GoToPageAsync("/leagues");
        await page.WaitForSelectorAsync("[aria-label='favorite']");
        await page.Locator("[aria-label='favorite']").First.ClickAsync();
        await page.WaitForSelectorAsync("[aria-label='unfavorite']");
        var unfavoriteButtons = await page.QuerySelectorAllAsync("[aria-label='unfavorite']");
        Assert.NotEmpty(unfavoriteButtons);
    }
}
```

- [ ] **Step 2: Create ClubFavoritingTests**

Create `tests/Personal.Dashboard.Feature.Tests/Clubs/ClubFavoritingTests.cs`:

```csharp
using Personal.Dashboard.Feature.Tests.Fixtures;

namespace Personal.Dashboard.Feature.Tests.Clubs;

[Collection("Application")]
public class ClubFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task WhenNoLeagueFavoritedThenClubsPageShowsEmptyState()
    {
        var page = await playwright.GoToPageAsync("/clubs");
        await page.WaitForSelectorAsync(".mud-list-item");
        var content = await page.ContentAsync();
        Assert.Contains("clubs appear after favoriting a league", content);
    }

    [Fact]
    public async Task WhenLeagueFavoritedThenClubsAppearOnClubsPage()
    {
        var leaguePage = await playwright.GoToPageAsync("/leagues");
        await leaguePage.WaitForSelectorAsync("[aria-label='favorite']");
        await leaguePage.Locator("[aria-label='favorite']").First.ClickAsync();

        var clubsPage = await playwright.GoToPageAsync("/clubs");
        await clubsPage.WaitForSelectorAsync(".mud-list-item");
        var items = await clubsPage.QuerySelectorAllAsync(".mud-list-item");
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task WhenClubFavoriteButtonClickedThenClubIsMarkedFavorite()
    {
        var leaguePage = await playwright.GoToPageAsync("/leagues");
        await leaguePage.WaitForSelectorAsync("[aria-label='favorite']");
        await leaguePage.Locator("[aria-label='favorite']").First.ClickAsync();

        var clubsPage = await playwright.GoToPageAsync("/clubs");
        await clubsPage.WaitForSelectorAsync("[aria-label='favorite']");
        await clubsPage.Locator("[aria-label='favorite']").First.ClickAsync();
        await clubsPage.WaitForSelectorAsync("[aria-label='unfavorite']");
        var unfavoriteButtons = await clubsPage.QuerySelectorAllAsync("[aria-label='unfavorite']");
        Assert.NotEmpty(unfavoriteButtons);
    }
}
```

- [ ] **Step 3: Build and verify the project compiles**

```bash
dotnet build tests/Personal.Dashboard.Feature.Tests --configuration Release
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add tests/Personal.Dashboard.Feature.Tests/Leagues/ \
        tests/Personal.Dashboard.Feature.Tests/Clubs/
git commit -m "feat: add Playwright feature tests for league and club favoriting"
```
