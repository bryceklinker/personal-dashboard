[Collection("Application")]
public class ClubFavoritingTests(ApplicationFixture app, PlaywrightFixture playwright)
    : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task WhenClubFavoritedThenShowsAsFavorite()
    {
        var leaguePage = await playwright.NewPageAsync();
        await playwright.GoToPageAsync(leaguePage, "/leagues");

        await leaguePage.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await leaguePage.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(leaguePage.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();

        var page = await playwright.NewPageAsync();
        await playwright.GoToPageAsync(page, "/clubs");

        await page.Locator("button[aria-label='favorite']").First.WaitForAsync();
        await page.Locator("button[aria-label='favorite']").First.ClickAsync();

        await Expect(page.Locator("button[aria-label='unfavorite']").First).ToBeVisibleAsync();
    }
}
