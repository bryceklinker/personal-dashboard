using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Leagues.Entities;

public class FootballCountry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Flag { get; set; }

    public ICollection<FootballCountryAlias> Aliases { get; set; } = new List<FootballCountryAlias>();
    public ICollection<FootballLeagueEntity> Leagues { get; set; } = new List<FootballLeagueEntity>();

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballCountryAlias
        {
            AliasSource = source,
            Alias = alias,
            Country = this,
        });
    }

    public static FootballCountry CreateFromFootballApi(FootballApiCountry apiCountry)
    {
        var entity = new FootballCountry();
        entity.AddAlias(DataSource.FootballApi, apiCountry.Name.ToLowerInvariant());
        entity.UpdateFromFootballApi(apiCountry);
        return entity;
    }

    public void UpdateFromFootballApi(FootballApiCountry apiCountry)
    {
        Name = apiCountry.Name;
        Code = apiCountry.Code;
        Flag = apiCountry.Flag;
    }
}

public class FootballCountryConfiguration : IEntityTypeConfiguration<FootballCountry>
{
    public void Configure(EntityTypeBuilder<FootballCountry> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Code);
        builder.Property(c => c.Flag);

        builder.HasMany(c => c.Aliases)
            .WithOne(a => a.Country)
            .HasForeignKey(a => a.CountryId);
    }
}

public class FootballCountryAlias
{
    public string Alias { get; set; } = "";
    public string AliasSource { get; set; } = "";
    public Guid CountryId { get; set; } = Guid.Empty;

    public required FootballCountry Country { get; set; }
}

public class FootballCountryAliasConfiguration : IEntityTypeConfiguration<FootballCountryAlias>
{
    public void Configure(EntityTypeBuilder<FootballCountryAlias> builder)
    {
        builder.Property(a => a.Alias).IsRequired();
        builder.Property(a => a.AliasSource).IsRequired();

        builder.HasKey(a => new
        {
            a.AliasSource,
            a.Alias,
            a.CountryId
        });

        // Relationship is configured from FootballCountryConfiguration.HasMany(...)
        // Do not configure HasOne/WithMany here to avoid duplicate registration.
    }
}
