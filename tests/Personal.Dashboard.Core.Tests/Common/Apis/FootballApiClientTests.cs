using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;
using Personal.Dashboard.Test.Support.Common.Logging;

namespace Personal.Dashboard.Core.Tests.Common.Apis;

public class FootballApiClientTests
{
    private const string ApiKey = "the-api-key";
    private const string BaseUrl = "https://api.football.com";

    private readonly FakeLogger _logger;
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

        _logger = provider.GetRequiredService<FakeLogger>();
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
            [
                FootballApiDataFactory.League() with
                {
                    League = FootballApiDataFactory.LeagueInfo() with
                    {
                        Name = "Premier League"
                    }
                }
            ]
        );
        await _handler.SetupGetJsonResponseAsync($"{BaseUrl}/leagues", response);

        var actual = await _client.GetLeaguesAsync();

        Assert.Empty(actual.Errors);
        Assert.Equal("Premier League", actual.Response[0].League.Name);
    }

    [Fact]
    public async Task WhenGettingLeaguesReturnsAnErrorThenThrowsError()
    {
        var response = FootballApiDataFactory.FailureResponse<FootballApiLeaguesParameters, FootballApiLeague[]>(
            FootballApiLeaguesParameters.Empty(),
            [],
            [FootballApiDataFactory.Error()]
        );
        await _handler.SetupGetJsonResponseAsync($"{BaseUrl}/leagues", response);

        await Assert.ThrowsAsync<FootballApiException<FootballApiLeaguesParameters, FootballApiLeague[]>>(() =>
            _client.GetLeaguesAsync());
    }

    [Fact]
    public async Task WhenGettingLeaguesReturnsAnErrorThenLogsError()
    {
        var response = FootballApiDataFactory.FailureResponse<FootballApiLeaguesParameters, FootballApiLeague[]>(
            FootballApiLeaguesParameters.Empty(),
            [],
            [FootballApiDataFactory.Error()]
        );
        await _handler.SetupGetJsonResponseAsync($"{BaseUrl}/leagues", response);

        await Assert.ThrowsAnyAsync<Exception>(() => _client.GetLeaguesAsync());
        Assert.Single(_logger.GetLogItems(LogLevel.Error));
    }
    
    [Fact]
    public async Task WhenGettingLeaguesWithParametersThenParametersAreInQueryString()
    {
        HttpRequestMessage? request = null;
        await _handler.SetupGetJsonResponseAsync($"{BaseUrl}/leagues",
            FootballApiDataFactory.SuccessResponse<FootballApiLeaguesParameters, FootballApiLeague[]>(
                FootballApiLeaguesParameters.Empty(),
                []
            ), new ConfigureResponseOptions
            {
                Capture = req => request = req
            }
        );
        
        await _client.GetLeaguesAsync(new FootballApiLeaguesParameters(
            Id: 1,
            Name: "Three",
            Country: "USA",
            Code: "Cod",
            Season: 2012,
            Team: 4,
            Type: "league",
            Current: true,
            Search: "Bob",
            Last: "Wilson"
        ));
        
        var queryString = request.ParseQueryString();
        Assert.Contains("1", queryString.Get("id"));
        Assert.Contains("three", queryString.Get("name"));
        Assert.Contains("usa", queryString.Get("country"));
        Assert.Contains("cod", queryString.Get("code"));
        Assert.Contains("2012", queryString.Get("season"));
        Assert.Contains("4", queryString.Get("team"));
        Assert.Contains("league", queryString.Get("type"));
        Assert.Contains("true", queryString.Get("current"));
        Assert.Contains("bob", queryString.Get("search"));
        Assert.Contains("wilson", queryString.Get("last"));
    }
}