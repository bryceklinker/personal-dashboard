using AngleSharp.Dom;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Leagues;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Leagues;

public class LeaguesListTests
{
    [Fact]
    public async Task WhenTotalIsLessThanLimitThenDisablesNext()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupLeagues();

        var page = context.Render<LeaguesList>();
        var nextButton = page.FindByRole("button", new FindByRoleOptions(Label: "next"));

        Assert.True(nextButton.IsDisabled());
    }
    
    [Fact]
    public async Task WhenGoingToNextPageThenGetsNextPageOfLeagues()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? request = null;
        await context.HttpHandler.SetupLeagues(
            total: 100,
            limit: 10,
            leagues: DataFactory.Many(DataFactory.FootballLeagueModel, 10),
            options: new ConfigureResponseOptions(
                Capture: req => request = req 
            )
        );

        var page = context.Render<LeaguesList>();
        await page.FindByRole("button", new FindByRoleOptions(Label: "next")).ClickAsync();

        await Eventually.Assert(() =>
        {
            var queryParams = request?.ParseQueryString();
            Assert.Equal("10", queryParams?.Get("offset"));
        });
    }

    [Fact]
    public async Task WhenGoingToPreviousPageThenGetsPreviousPageOfLeagues()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? request = null;
        await context.HttpHandler.SetupLeagues(
            total: 100,
            limit: 10,
            offset: 10,
            leagues: DataFactory.Many(DataFactory.FootballLeagueModel, 10),
            options: new ConfigureResponseOptions(
                Capture: req => request = req 
            )
        );

        var page = context.Render<LeaguesList>();
        await page.FindByRole("button", new FindByRoleOptions(Label: "previous")).ClickAsync();
        
        await Eventually.Assert(() =>
        {
            var queryParams = request?.ParseQueryString();
            Assert.Equal("0", queryParams?.Get("offset"));
        });
    }

    [Fact]
    public async Task WhenRefreshButtonClickedThenCallsRefreshEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? refreshRequest = null;
        await context.HttpHandler.SetupLeagues();
        await context.HttpHandler.SetupRefreshLeagues(
            new ConfigureResponseOptions(Capture: req => refreshRequest = req)
        );

        var page = context.Render<LeaguesList>();
        await page.FindByRole("button", new FindByRoleOptions(Label: "refresh")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(refreshRequest));
    }

    [Fact]
    public async Task WhenLeaguesRefreshedEventReceivedThenRefetchesLeagues()
    {
        await using var context = new PersonalDashboardWebContext();
        var refreshedLeague = DataFactory.FootballLeagueModel();
        await context.HttpHandler.SetupLeagues(leagues: [refreshedLeague]);

        var page = context.Render<LeaguesList>();
        await context.HubFactory.Connection.SimulateEventAsync(new LeaguesRefreshedDashboardEvent());

        await Eventually.Assert(() =>
            Assert.Contains(page.FindAll(".mud-list-item"),
                item => item.TextContent.Contains(refreshedLeague.Name)));
    }

    [Fact]
    public async Task WhenLeagueHasLastRefreshedThenDisplaysIt()
    {
        await using var context = new PersonalDashboardWebContext();
        var lastRefreshed = DateTimeOffset.UtcNow.AddHours(-2);
        var league = DataFactory.FootballLeagueModel() with { LastRefreshed = lastRefreshed };
        await context.HttpHandler.SetupLeagues(leagues: [league]);

        var page = context.Render<LeaguesList>();

        await Eventually.Assert(() =>
            Assert.Contains(lastRefreshed.ToString("g"), page.Markup));
    }
}