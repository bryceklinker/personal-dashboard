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

        var leagueFavoriteButton = page.GetByRole(AriaRole.Button, new() { Name = "favorite" }).First;
        await leagueFavoriteButton.WaitForAsync();
        await leagueFavoriteButton.ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "unfavorite" }).First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

        await playwright.GoToPageAsync(page, "/clubs");

        var clubFavoriteButton = page.GetByRole(AriaRole.Button, new() { Name = "favorite" }).First;
        await clubFavoriteButton.WaitForAsync();
        await clubFavoriteButton.ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "unfavorite" }).First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
    }
}
