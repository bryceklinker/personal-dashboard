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
