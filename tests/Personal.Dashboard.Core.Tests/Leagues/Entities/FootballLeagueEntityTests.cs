using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Countries.Entities;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Entities;

public class FootballLeagueEntityTests
{
    [Fact]
    public void WhenFavoriteCalledThenIsFavoriteIsTrue()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Favorite();
        Assert.True(league.IsFavorite);
    }

    [Fact]
    public void WhenUnfavoriteCalledThenIsFavoriteIsFalse()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        league.Favorite();
        league.Unfavorite();
        Assert.False(league.IsFavorite);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenSetsNameAndLastRefreshed()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague();
        var apiLeague = FootballApiDataFactory.League();

        league.UpdateFromFootballApi(apiLeague, FootballCountryEntity.CreateFromFootballApi(apiLeague.Country));

        Assert.Equal(apiLeague.League.Name, league.Name);
        Assert.NotNull(league.LastRefreshed);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenUpsertsSeasons()
    {
        var entity = PersonalDashboardEntityFactory.FootballLeague();
        var apiLeague = FootballApiDataFactory.League();

        entity.UpdateFromFootballApi(apiLeague, FootballCountryEntity.CreateFromFootballApi(apiLeague.Country));

        Assert.Equal(apiLeague.Seasons.Length, entity.Seasons.Count);
    }

    [Fact]
    public void WhenCreateFromFootballApiCalledThenSetsCountry()
    {
        var apiLeague = FootballApiDataFactory.League();
        var country = FootballCountryEntity.CreateFromFootballApi(apiLeague.Country);

        var entity = FootballLeagueEntity.CreateFromFootballApi(apiLeague, country);

        Assert.Equal(country, entity.Country);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenUpdatesCountry()
    {
        var apiLeague = FootballApiDataFactory.League();
        var originalCountry = FootballCountryEntity.CreateFromFootballApi(apiLeague.Country);
        var entity = FootballLeagueEntity.CreateFromFootballApi(apiLeague, originalCountry);

        var newCountry = FootballCountryEntity.CreateFromFootballApi(FootballApiDataFactory.Country());
        entity.UpdateFromFootballApi(FootballApiDataFactory.League(), newCountry);

        Assert.Equal(newCountry, entity.Country);
    }
}
