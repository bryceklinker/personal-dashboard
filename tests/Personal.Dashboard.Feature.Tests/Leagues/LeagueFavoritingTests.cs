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

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "unfavorite" }).First).ToBeVisibleAsync();
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

        var favoriteButton = page.GetByRole(AriaRole.Button, new() { Name = "favorite" }).First;
        await favoriteButton.WaitForAsync();
        await favoriteButton.ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "unfavorite" }).First).ToBeVisibleAsync();
    }
}
