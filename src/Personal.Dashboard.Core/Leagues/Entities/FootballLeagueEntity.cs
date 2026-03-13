using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueEntity
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = "";
    public int? CurrentSeasonYear { get; set; }
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
        var currentSeason = league.Seasons.FirstOrDefault(s => s.Current)
            ?? league.Seasons.MaxBy(s => s.Year);
        CurrentSeasonYear = (int?)currentSeason?.Year;
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
