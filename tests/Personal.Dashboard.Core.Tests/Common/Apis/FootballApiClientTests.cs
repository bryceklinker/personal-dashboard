using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Common.Apis;

public class FootballApiClientTests
{
    private const string ApiKey = "the-api-key";
    private const string BaseUrl = "https://api.football.com";

    private readonly FakeHttpMessageHandler _handler;
    private readonly IFootballApiClient _client;

    public FootballApiClientTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(opts =>
        {
            opts.ConfigureFootballApi = api =>
            {
                api.ApiKey = ApiKey;
                api.BaseUrl = BaseUrl;
            };
        });

        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
        _client = provider.GetRequiredService<IFootballApiClient>();
    }

    [Fact]
    public async Task WhenGettingLeaguesThenUsesTheConfiguredApiKey()
    {
        HttpRequestMessage? request = null;
        await _handler.SetupGetJsonResponseAsync(
            $"{BaseUrl}/leagues",
            FootballApiDataFactory.SuccessResponse<FootballApiLeaguesParameters, FootballApiLeague[]>(
                FootballApiLeaguesParameters.Empty(),
                [FootballApiDataFactory.League()]
            ),
            new ConfigureResponseOptions
            {
                Capture = req => request = req
            }
        );
        await _client.GetLeaguesAsync();

        var requestHeaders = request?.Headers.GetValues("x-apisports-key") ?? [];
        Assert.Contains(requestHeaders, v => v == ApiKey);
    }

    [Fact]
    public async Task WhenGettingLeaguesThenReturnsLeaguesFromApi()
    {
        var response = FootballApiDataFactory.SuccessResponse<FootballApiLeaguesParameters, FootballApiLeague[]>(
            FootballApiLeaguesParameters.Empty(),
            [FootballApiDataFactory.League()]
        );
        await _handler.SetupGetJsonResponseAsync($"{BaseUrl}/leagues", response);

        var actual = await _client.GetLeaguesAsync();

        Assert.Empty(actual.Errors);
    }
}