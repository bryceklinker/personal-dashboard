using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Countries.Entities;

public class FootballCountryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Flag { get; set; }

    public ICollection<FootballCountryAlias> Aliases { get; set; } = new List<FootballCountryAlias>();

    public void AddAlias(string source, string alias)
    {
        Aliases.Add(new FootballCountryAlias
        {
            AliasSource = source,
            Alias = alias,
            Country = this,
        });
    }

    public static FootballCountryEntity CreateFromFootballApi(FootballApiCountry apiCountry)
    {
        var entity = new FootballCountryEntity();
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

public class FootballCountryEntityConfiguration : IEntityTypeConfiguration<FootballCountryEntity>
{
    public void Configure(EntityTypeBuilder<FootballCountryEntity> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Code);
        builder.Property(c => c.Flag);
    }
}
