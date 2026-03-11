[Collection("Application")]
public class ClubFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    private readonly ApplicationFixture _app = app;

    [Fact]
    public async Task WhenClubFavoritedThenShowsAsFavorite()
    {
        var page = await playwright.NewPageAsync();
        await playwright.GoToPageAsync(page, "/leagues");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();

        await playwright.GoToPageAsync(page, "/clubs");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }
}
