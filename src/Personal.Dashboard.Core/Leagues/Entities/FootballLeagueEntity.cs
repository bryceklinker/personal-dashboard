using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public class FootballLeagueEntityConfiguration : IEntityTypeConfiguration<FootballLeagueEntity>
{
    public void Configure(EntityTypeBuilder<FootballLeagueEntity> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(p => p.Name).IsRequired();
    }
}