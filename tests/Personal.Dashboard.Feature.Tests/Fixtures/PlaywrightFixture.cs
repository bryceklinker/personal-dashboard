public class PlaywrightFixture : IAsyncLifetime
{
    private IBrowser? _browser;
    private IPlaywright? _playwright;
    private readonly ApplicationFixture _app;
    private readonly List<IBrowserContext> _contexts = [];

    public PlaywrightFixture(ApplicationFixture app)
    {
        _app = app;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await (_browser ?? throw new InvalidOperationException("Browser not initialized"))
            .NewContextAsync(new BrowserNewContextOptions
            {
                RecordVideoDir = "playwright-videos",
                RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
            });
        _contexts.Add(context);
        return await context.NewPageAsync();
    }

    public async Task GoToPageAsync(IPage page, string path) =>
        await page.GotoAsync($"{_app.GetEndpoint("web").TrimEnd('/')}/{path.TrimStart('/')}");

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts)
            await context.CloseAsync();

        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
