using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Tests.Clubs.Entities;

public class FootballClubEntityTests
{
    [Fact]
    public void WhenFavoriteCalledThenIsFavoriteIsTrue()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.Favorite();
        Assert.True(club.IsFavorite);
    }

    [Fact]
    public void WhenUnfavoriteCalledThenIsFavoriteIsFalse()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.Favorite();
        club.Unfavorite();
        Assert.False(club.IsFavorite);
    }

    [Fact]
    public void WhenAddAliasThenAliasIsAdded()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        club.AddAlias(DataSource.FootballApi, "42");
        Assert.Single(club.Aliases);
        Assert.Equal("42", club.Aliases.First().Alias);
    }

    [Fact]
    public void WhenAddLeagueThenLeagueIsAdded()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        var league = PersonalDashboardEntityFactory.FootballLeague();
        club.AddLeague(league);
        Assert.Single(club.Leagues);
    }

    [Fact]
    public void WhenUpdateFromFootballApiCalledThenSetsNameAndLastRefreshed()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        var team = FootballApiDataFactory.Team();

        club.UpdateFromFootballApi(team);

        Assert.Equal(team.Team.Name, club.Name);
        Assert.NotNull(club.LastRefreshed);
    }
}
