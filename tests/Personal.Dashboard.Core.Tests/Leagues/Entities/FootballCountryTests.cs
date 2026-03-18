using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Entities;

public class FootballCountryTests
{
    [Fact]
    public void WhenCreateFromFootballApiCalledThenStoresNameCodeFlag()
    {
        var apiCountry = FootballApiDataFactory.Country();

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Equal(apiCountry.Name, country.Name);
        Assert.Equal(apiCountry.Code, country.Code);
        Assert.Equal(apiCountry.Flag, country.Flag);
    }

    [Fact]
    public void WhenCreateFromFootballApiCalledThenAddsLowercasedAlias()
    {
        var apiCountry = FootballApiDataFactory.Country() with { Name = "England" };

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Single(country.Aliases, a =>
            a.AliasSource == DataSource.FootballApi && a.Alias == "england");
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenUpdatesNameCodeFlag()
    {
        var country = FootballCountry.CreateFromFootballApi(FootballApiDataFactory.Country());
        var updated = FootballApiDataFactory.Country();

        country.UpdateFromFootballApi(updated);

        Assert.Equal(updated.Name, country.Name);
        Assert.Equal(updated.Code, country.Code);
        Assert.Equal(updated.Flag, country.Flag);
    }

    [Fact]
    public void WhenCountryHasNullCodeAndFlagThenCreateFromFootballApiSucceeds()
    {
        var apiCountry = new FootballApiCountry("World", null, null);

        var country = FootballCountry.CreateFromFootballApi(apiCountry);

        Assert.Null(country.Code);
        Assert.Null(country.Flag);
        Assert.Single(country.Aliases, a => a.Alias == "world");
    }
}
