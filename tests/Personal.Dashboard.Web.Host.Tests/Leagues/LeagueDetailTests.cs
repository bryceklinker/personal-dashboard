using Microsoft.AspNetCore.Components;
using MudBlazor;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Web.Host.Leagues;
using Personal.Dashboard.Web.Host.Tests.Support;

namespace Personal.Dashboard.Web.Host.Tests.Leagues;

public class LeagueDetailTests
{
    [Fact]
    public async Task WhenLeagueIsNullThenShowsPlaceholder()
    {
        await using var context = new PersonalDashboardWebContext();

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, null));

        Assert.Contains("Select a league to view details", page.Markup);
    }

    [Fact]
    public async Task WhenLeagueIsSetThenShowsLeagueName()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel();

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains(league.Name, page.Markup);
    }

    [Fact]
    public async Task WhenLeagueHasCurrentSeasonThenShowsCurrentSeasonCard()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with
        {
            Seasons = [new FootballLeagueSeasonModel(2025, true)]
        };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("2025", page.Markup);
        Assert.Contains("2026", page.Markup); // Year+1 displayed
    }

    [Fact]
    public async Task WhenLeagueHasMultipleSeasonsThenShowsAllSeasons()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with
        {
            Seasons =
            [
                new FootballLeagueSeasonModel(2025, true),
                new FootballLeagueSeasonModel(2024, false),
                new FootballLeagueSeasonModel(2023, false)
            ]
        };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("2025", page.Markup);
        Assert.Contains("2024", page.Markup);
        Assert.Contains("2023", page.Markup);
    }

    [Fact]
    public async Task WhenLeagueHasCountryThenShowsCountryNameAndCode()
    {
        await using var context = new PersonalDashboardWebContext();
        var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
        var league = DataFactory.FootballLeagueModel() with { Country = country };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("England", page.Markup);
        Assert.Contains("GB", page.Markup);
    }

    [Fact]
    public async Task WhenLeagueHasCountryFlagThenShowsFlagImage()
    {
        await using var context = new PersonalDashboardWebContext();
        var country = new FootballCountryModel("England", "GB", "https://flags.example.com/gb.svg");
        var league = DataFactory.FootballLeagueModel() with { Country = country };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("https://flags.example.com/gb.svg", page.Markup);
    }

    [Fact]
    public async Task WhenLeagueHasNullCountryThenDoesNotShowCountrySection()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { Country = null };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.DoesNotContain("<img", page.Markup);
    }

    [Fact]
    public async Task WhenLeagueCountryHasNullCodeThenShowsOnlyName()
    {
        await using var context = new PersonalDashboardWebContext();
        var country = new FootballCountryModel("World", null, null);
        var league = DataFactory.FootballLeagueModel() with { Country = country };

        var page = context.Render<LeagueDetail>(p => p.Add(x => x.League, league));

        Assert.Contains("World", page.Markup);
        Assert.DoesNotContain("·", page.Markup);
    }

    [Fact]
    public async Task WhenFavoriteToggledThenInvokesOnFavoriteChanged()
    {
        await using var context = new PersonalDashboardWebContext();
        var league = DataFactory.FootballLeagueModel() with { IsFavorite = false };
        await context.HttpHandler.SetupFavoriteLeague(league.Id);

        var callbackInvoked = false;
        var page = context.Render<LeagueDetail>(p =>
        {
            p.Add(x => x.League, league);
            p.Add(x => x.OnFavoriteChanged, EventCallback.Factory.Create(context, () => callbackInvoked = true));
        });

        await page.FindByRole("button", new FindByRoleOptions(Label: "favorite")).ClickAsync();

        await Eventually.Assert(() => Assert.True(callbackInvoked));
    }
}
