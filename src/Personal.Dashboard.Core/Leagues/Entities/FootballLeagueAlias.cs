using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballLeagueAlias
{
    public string Alias { get; set; }
    public string AliasSource { get; set; }
    public Guid LeagueId { get; set; }
    
    public FootballLeagueEntity League { get; set; }
}

public class FootballLeagueAliasConfiguration : IEntityTypeConfiguration<FootballLeagueAlias>
{
    public void Configure(EntityTypeBuilder<FootballLeagueAlias> builder)
    {
        builder.Property(a => a.Alias).IsRequired();
        builder.Property(a => a.AliasSource).IsRequired();

        builder.HasOne(a => a.League)
            .WithMany(a => a.Aliases)
            .HasForeignKey(a => a.LeagueId);

        builder.HasKey(a => new
        {
            a.AliasSource,
            a.Alias,
            a.LeagueId
        });
    }
}