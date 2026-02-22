using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    public ICollection<FootballLeagueAlias> Aliases { get; set; } = new List<FootballLeagueAlias>();

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballLeagueAlias
        {
            AliasSource = source,
            Alias = alias,
            League = this,
        });
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