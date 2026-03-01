using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Leagues;

public class LeaguesPageTests
{
    [Fact]
    public async Task WhenRenderedThenShowsListOfLeagues()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupLeagues(leagues:
        [
            DataFactory.FootballLeagueModel(),
            DataFactory.FootballLeagueModel(),
            DataFactory.FootballLeagueModel()
        ]);

        var page = context.Render<Host.Leagues.Leagues>();
        Assert.Equal(3, page.FindComponents<MudListItem<FootballLeagueModel>>().Count);
    }
}