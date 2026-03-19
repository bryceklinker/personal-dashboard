using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Countries.Entities;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset? LastRefreshed { get; set; }
    public bool IsFavorite { get; set; }
    public Guid? CountryId { get; set; }
    public FootballCountryEntity? Country { get; set; }

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

    public static FootballLeagueEntity CreateFromFootballApi(FootballApiLeague apiLeague, FootballCountryEntity country)
    {
        var entity = new FootballLeagueEntity();
        entity.AddAlias(DataSource.FootballApi, $"{apiLeague.League.Id}");
        entity.UpdateFromFootballApi(apiLeague, country);
        return entity;
    }

    public void UpdateFromFootballApi(FootballApiLeague apiLeague, FootballCountryEntity country)
    {
        Name = apiLeague.League.Name;
        LastRefreshed = DateTimeOffset.UtcNow;
        Country = country;
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

        builder.HasOne(p => p.Country)
            .WithMany()
            .HasForeignKey(p => p.CountryId)
            .IsRequired(false);
    }
}
