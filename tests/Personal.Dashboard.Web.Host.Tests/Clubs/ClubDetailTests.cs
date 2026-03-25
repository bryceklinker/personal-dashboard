using Microsoft.AspNetCore.Components;
using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Web.Host.Clubs;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Clubs;

public class ClubDetailTests
{
    [Fact]
    public async Task WhenClubIsNullThenShowsPlaceholder()
    {
        await using var context = new PersonalDashboardWebContext();

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, null));

        Assert.Contains("Select a club to view details", page.Markup);
    }

    [Fact]
    public async Task WhenClubIsSetThenShowsClubName()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel();

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));

        Assert.Contains(club.Name, page.Markup);
    }

    [Fact]
    public async Task WhenClubBelongsToLeagueThenShowsLeagueName()
    {
        await using var context = new PersonalDashboardWebContext();
        var leagueName = "Premier League";
        var club = DataFactory.FootballClubModel() with
        {
            Leagues = [new FootballClubLeagueModel(Guid.NewGuid(), leagueName)]
        };

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));

        Assert.Contains(leagueName, page.Markup);
    }

    [Fact]
    public async Task WhenFavoriteToggledThenCallsFavoriteEndpoint()
    {
        await using var context = new PersonalDashboardWebContext();
        var club = DataFactory.FootballClubModel() with { IsFavorite = false };
        HttpRequestMessage? captured = null;
        await context.HttpHandler.SetupFavoriteClub(
            club.Id,
            options: new ConfigureResponseOptions(Capture: req => captured = req));

        var page = context.Render<ClubDetail>(p => p.Add(x => x.Club, club));
        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.NotNull(captured));
    }
}
