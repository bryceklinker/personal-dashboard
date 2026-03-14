using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Clubs;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Clubs;

public class ClubsListTests
{
    [Fact]
    public async Task WhenTotalIsLessThanLimitThenDisablesNext()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupClubs();

        var page = context.Render<ClubsList>();
        var nextButton = page.FindByRole("button", new FindByRoleOptions(Label: "next"));

        Assert.True(nextButton.IsDisabled());
    }

    [Fact]
    public async Task WhenOnLastPageThenDisablesNext()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupClubs(
            total: 20,
            limit: 10,
            offset: 10,
            clubs: DataFactory.Many(DataFactory.FootballClubModel, 10)
        );

        var page = context.Render<ClubsList>();
        await Eventually.Assert(() =>
            Assert.False(page.FindByRole("button", new FindByRoleOptions(Label: "next")).IsDisabled()));
        await page.FindByRole("button", new FindByRoleOptions(Label: "next")).ClickAsync();

        await Eventually.Assert(() =>
            Assert.True(page.FindByRole("button", new FindByRoleOptions(Label: "next")).IsDisabled()));
    }

    [Fact]
    public async Task WhenGoingToNextPageThenGetsNextPageOfClubs()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? request = null;
        await context.HttpHandler.SetupClubs(
            total: 100,
            limit: 10,
            clubs: DataFactory.Many(DataFactory.FootballClubModel, 10),
            options: new ConfigureResponseOptions(
                Capture: req => request = req
            )
        );

        var page = context.Render<ClubsList>();
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
    public async Task WhenGoingToPreviousPageThenGetsPreviousPageOfClubs()
    {
        await using var context = new PersonalDashboardWebContext();
        HttpRequestMessage? request = null;
        await context.HttpHandler.SetupClubs(
            total: 100,
            limit: 10,
            offset: 10,
            clubs: DataFactory.Many(DataFactory.FootballClubModel, 10),
            options: new ConfigureResponseOptions(
                Capture: req => request = req
            )
        );

        var page = context.Render<ClubsList>();
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
        await context.HttpHandler.SetupClubs();
        await context.HttpHandler.SetupRefreshClubs(
            new ConfigureResponseOptions(Capture: req => refreshRequest = req)
        );

        var page = context.Render<ClubsList>();
        await page.FindByRole("button", new FindByRoleOptions(Label: "refresh")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(refreshRequest));
    }

    [Fact]
    public async Task WhenClubsRefreshedEventReceivedThenRefetchesClubs()
    {
        await using var context = new PersonalDashboardWebContext();
        var refreshedClub = DataFactory.FootballClubModel();
        await context.HttpHandler.SetupClubs(clubs: [refreshedClub]);

        var page = context.Render<ClubsList>();
        await context.HubFactory.Connection.SimulateEventAsync(new ClubsRefreshedDashboardEvent());

        await Eventually.Assert(() =>
            Assert.Contains(page.FindComponents<MudListItem<FootballClubModel>>(),
                item => item.Markup.Contains(refreshedClub.Name)));
    }

    [Fact]
    public async Task WhenClubHasLastRefreshedThenDisplaysIt()
    {
        await using var context = new PersonalDashboardWebContext();
        var lastRefreshed = DateTimeOffset.UtcNow.AddHours(-2);
        var club = DataFactory.FootballClubModel() with { LastRefreshed = lastRefreshed };
        await context.HttpHandler.SetupClubs(clubs: [club]);

        var page = context.Render<ClubsList>();

        await Eventually.Assert(() =>
            Assert.Contains(lastRefreshed.ToString("g"), page.Markup));
    }

    [Fact]
    public async Task WhenNoClubsExistThenDisplaysEmptyState()
    {
        await using var context = new PersonalDashboardWebContext();
        await context.HttpHandler.SetupClubs(total: 0);

        var page = context.Render<ClubsList>();

        await Eventually.Assert(() =>
            Assert.Contains("Clubs appear after favoriting a league", page.Markup));
    }

    [Fact]
    public async Task WhenFavoriteToggledThenCallsFavoriteEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        HttpRequestMessage? favoriteRequest = null;
        await context.HttpHandler.SetupClubs(clubs: [club]);
        await context.HttpHandler.SetupFavoriteClub(
            club.Id,
            new ConfigureResponseOptions(Capture: req => favoriteRequest = req)
        );

        var page = context.Render<ClubsList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(favoriteRequest));
    }

    [Fact]
    public async Task WhenFavoriteToggledThenReloadsClubs()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        var reloadedClub = club with { Name = club.Name + " reloaded", IsFavorite = true };
        await context.HttpHandler.SetupClubs(clubs: [club]);
        await context.HttpHandler.SetupFavoriteClub(club.Id);

        var page = context.Render<ClubsList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await context.HttpHandler.SetupClubs(clubs: [reloadedClub]);
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.Contains(reloadedClub.Name, page.Markup));
    }

    [Fact]
    public async Task WhenFavoriteFailsThenShowsSnackbarError()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        await context.HttpHandler.SetupClubs(clubs: [club]);
        await context.HttpHandler.SetupFavoriteClub(
            club.Id,
            new ConfigureResponseOptions(Status: System.Net.HttpStatusCode.InternalServerError)
        );

        var page = context.Render<ClubsList>();
        await Eventually.Assert(() =>
            page.FindByRole("button", new FindByRoleOptions(Label: "favorite")));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() =>
            Assert.Contains(context.Snackbar.AddedMessages,
                m => m.Severity == Severity.Error));
    }

    [Fact]
    public async Task WhenClubRowClickedThenInvokesOnClubSelected()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel();
        await context.HttpHandler.SetupClubs(clubs: [club]);

        FootballClubModel? selected = null;
        var page = context.Render<ClubsList>(parameters =>
            parameters.Add(p => p.OnClubSelected, EventCallback.Factory.Create<FootballClubModel>(
                context, m => selected = m)));

        await Eventually.Assert(() =>
            Assert.True(page.FindComponents<MudListItem<FootballClubModel>>().Count > 0));

        await page.FindComponents<MudListItem<FootballClubModel>>()[0].Find("div[role='button']").ClickAsync();

        await Eventually.Assert(() => Assert.Equal(club.Id, selected?.Id));
    }
}
