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
