[Collection("Application")]
public class LeagueFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task WhenLeagueFavoritedThenShowsAsFavorite()
    {
        var page = await playwright.NewPageAsync();
        await playwright.GoToPageAsync(page, "/leagues");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task WhenLeagueFavoritedThenClubsAppearInClubsList()
    {
        var page = await playwright.NewPageAsync();
        await playwright.GoToPageAsync(page, "/leagues");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();

        await playwright.GoToPageAsync(page, "/clubs");

        await Expect(page.Locator("button[aria-label='favorite'], button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }
}
