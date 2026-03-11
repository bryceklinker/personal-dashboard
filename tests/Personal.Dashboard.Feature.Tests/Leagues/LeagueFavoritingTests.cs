[Collection("Application")]
public class LeagueFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    private readonly ApplicationFixture _app = app;

    [Fact]
    public async Task WhenLeagueFavoritedThenShowsAsFavorite()
    {
        var page = await playwright.NewPageAsync();
        await FavoriteFirstUnfavoritedLeagueAsync(page);

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task WhenLeagueFavoritedThenClubsAppearInClubsList()
    {
        var page = await playwright.NewPageAsync();
        await FavoriteFirstUnfavoritedLeagueAsync(page);

        await playwright.GoToPageAsync(page, "/clubs");

        await Expect(page.GetByText("Clubs appear after favoriting a league")).ToBeHiddenAsync();
    }

    private async Task FavoriteFirstUnfavoritedLeagueAsync(IPage page)
    {
        await playwright.GoToPageAsync(page, "/leagues");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }
}
