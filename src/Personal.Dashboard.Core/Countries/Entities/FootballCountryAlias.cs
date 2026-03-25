using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Personal.Dashboard.Core.Countries.Entities;

public class FootballCountryAlias
{
    public string Alias { get; set; } = "";
    public string AliasSource { get; set; } = "";
    public Guid CountryId { get; set; } = Guid.Empty;

    public required FootballCountryEntity Country { get; set; }
}

public class FootballCountryAliasConfiguration : IEntityTypeConfiguration<FootballCountryAlias>
{
    public void Configure(EntityTypeBuilder<FootballCountryAlias> builder)
    {
        builder.Property(a => a.Alias).IsRequired();
        builder.Property(a => a.AliasSource).IsRequired();

        builder.HasOne(a => a.Country)
            .WithMany(a => a.Aliases)
            .HasForeignKey(a => a.CountryId);

        builder.HasKey(a => new
        {
            a.AliasSource,
            a.Alias,
            a.CountryId
        });
    }
}
