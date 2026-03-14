using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using MudBlazor;
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
    public async Task WhenOnLastPageThenDisablesNext()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupLeagues(
            total: 20,
            limit: 10,
            offset: 10,
            leagues: DataFactory.Many(DataFactory.FootballLeagueModel, 10)
        );

        var page = context.Render<LeaguesList>();
        await Eventually.Assert(() =>
            Assert.False(page.FindByRole("button", new FindByRoleOptions(Label: "next")).IsDisabled()));
        await page.FindByRole("button", new FindByRoleOptions(Label: "next")).ClickAsync();

        await Eventually.Assert(() =>
            Assert.True(page.FindByRole("button", new FindByRoleOptions(Label: "next")).IsDisabled()));
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
        await Eventually.Assert(() =>
            Assert.False(page.FindByRole("button", new FindByRoleOptions(Label: "next")).IsDisabled()));
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
            Assert.Contains(page.FindComponents<MudListItem<FootballLeagueModel>>(),
                item => item.Markup.Contains(refreshedLeague.Name)));
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

    [Fact]
    public async Task WhenFavoriteToggledThenCallsFavoriteEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
        HttpRequestMessage? favoriteRequest = null;
        await context.HttpHandler.SetupLeagues(leagues: [league]);
        await context.HttpHandler.SetupFavoriteLeague(
            league.Id,
            new ConfigureResponseOptions(Capture: req => favoriteRequest = req)
        );

        var page = context.Render<LeaguesList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(favoriteRequest));
    }

    [Fact]
    public async Task WhenFavoriteToggledThenReloadsLeagues()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
        var reloadedLeague = league with { Name = league.Name + " reloaded", IsFavorite = true };
        await context.HttpHandler.SetupLeagues(leagues: [league]);
        await context.HttpHandler.SetupFavoriteLeague(league.Id);

        var page = context.Render<LeaguesList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await context.HttpHandler.SetupLeagues(leagues: [reloadedLeague]);
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.Contains(reloadedLeague.Name, page.Markup));
    }

    [Fact]
    public async Task WhenFavoriteFailsThenShowsSnackbarError()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
        await context.HttpHandler.SetupLeagues(leagues: [league]);
        await context.HttpHandler.SetupFavoriteLeague(
            league.Id,
            new ConfigureResponseOptions(Status: System.Net.HttpStatusCode.InternalServerError)
        );

        var page = context.Render<LeaguesList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() =>
            Assert.Contains(context.Snackbar.AddedMessages,
                m => m.Severity == Severity.Error));
    }

    [Fact]
    public async Task WhenLeagueRowClickedThenInvokesOnLeagueSelected()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel();
        await context.HttpHandler.SetupLeagues(leagues: [league]);

        FootballLeagueModel? selected = null;
        var page = context.Render<LeaguesList>(parameters =>
            parameters.Add(p => p.OnLeagueSelected, EventCallback.Factory.Create<FootballLeagueModel>(
                context, m => selected = m)));

        await Eventually.Assert(() =>
            Assert.True(page.FindComponents<MudListItem<FootballLeagueModel>>().Count > 0));

        await page.FindComponents<MudListItem<FootballLeagueModel>>()[0].Find("div[role='button']").ClickAsync();

        await Eventually.Assert(() => Assert.Equal(league.Id, selected?.Id));
    }

    [Fact]
    public async Task WhenLeagueHasCurrentSeasonThenDisplaysSeasonYear()
    {
        await using var context = new PersonalDashboardWebContext();
        var season = new FootballLeagueSeasonModel(2025, true);
        var league = DataFactory.FootballLeagueModel() with { Seasons = [season] };
        await context.HttpHandler.SetupLeagues(leagues: [league]);

        var page = context.Render<LeaguesList>();

        await Eventually.Assert(() => Assert.Contains("2025", page.Markup));
    }
}