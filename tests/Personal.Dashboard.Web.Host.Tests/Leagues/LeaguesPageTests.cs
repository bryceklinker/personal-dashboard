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
        await Eventually.Assert(() =>
            Assert.Equal(3, page.FindComponents<MudListItem<FootballLeagueModel>>().Count));
    }

    [Fact]
    public async Task WhenLeagueSelectedThenShowsDetailPanel()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel();
        await context.HttpHandler.SetupLeagues(leagues: [league]);

        var page = context.Render<Host.Leagues.Leagues>();
        await Eventually.Assert(() =>
            Assert.NotEmpty(page.FindComponents<MudListItem<FootballLeagueModel>>()));

        await page.FindComponents<MudListItem<FootballLeagueModel>>()[0]
            .Find("div[role='button']").ClickAsync();

        await Eventually.Assert(() => Assert.Contains(league.Name, page.Markup));
    }
}