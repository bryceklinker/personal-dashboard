using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;
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
    public Guid Id { get; set; } = Guid.NewGuid();
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

    public static FootballLeagueEntity CreateFromFootballApi(FootballApiLeague apiLeague)
    {
        var entity = new FootballLeagueEntity();
        entity.Aliases.Add(new FootballLeagueAlias
        {
            AliasSource = DataSource.FootballApi,
            Alias = $"{apiLeague.League.Id}",
            League = entity,
        });
        entity.UpdateFromFootballApi(apiLeague);
        return entity;
    }

    public void UpdateFromFootballApi(FootballApiLeague apiLeague)
    {
        Name = apiLeague.League.Name;
        LastRefreshed = DateTimeOffset.UtcNow;
        UpsertSeasons(apiLeague.Seasons);
    }

    private void UpsertSeasons(FootballApiSeason[] apiSeasons)
    {
        var knownYears = Seasons.ToDictionary(s => s.Year);
        foreach (var apiSeason in apiSeasons)
        {
            var year = (int)apiSeason.Year;
            if (knownYears.TryGetValue(year, out var existing))
                existing.IsCurrent = apiSeason.Current;
            else
            {
                var season = new FootballLeagueSeason { Year = year, IsCurrent = apiSeason.Current, League = this };
                Seasons.Add(season);
                knownYears[year] = season;
            }
        }
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
