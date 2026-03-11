public class PlaywrightFixture : IAsyncLifetime
{
    private IBrowser? _browser;
    private IPlaywright? _playwright;
    private readonly ApplicationFixture _app;

    public PlaywrightFixture(ApplicationFixture app)
    {
        _app = app;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync() =>
        await (_browser ?? throw new InvalidOperationException("Browser not initialized")).NewPageAsync();

    public async Task GoToPageAsync(IPage page, string path) =>
        await page.GotoAsync($"{_app.GetEndpoint("web").TrimEnd('/')}/{path.TrimStart('/')}");

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
