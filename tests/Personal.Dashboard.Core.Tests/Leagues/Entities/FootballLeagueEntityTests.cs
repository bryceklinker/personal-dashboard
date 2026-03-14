using Personal.Dashboard.Core.Common.Apis.FootballApi;
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

        league.UpdateFromFootballApi(apiLeague);

        Assert.Equal(apiLeague.League.Name, league.Name);
        Assert.NotNull(league.LastRefreshed);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenUpsertsSeasons()
    {
        var entity = PersonalDashboardEntityFactory.FootballLeague();
        var apiLeague = FootballApiDataFactory.League();

        entity.UpdateFromFootballApi(apiLeague);

        Assert.Equal(apiLeague.Seasons.Length, entity.Seasons.Count);
    }
}
