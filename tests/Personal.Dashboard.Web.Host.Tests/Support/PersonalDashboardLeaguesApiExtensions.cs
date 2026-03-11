using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public static class PersonalDashboardLeaguesApiExtensions
{
    public static async Task SetupLeagues(
        this FakeHttpMessageHandler handler,
        long offset = 0,
        long limit = 10,
        long total = 10,
        ConfigureResponseOptions? options = null,
        params FootballLeagueModel[] leagues
    )
    {
        var result = DataFactory.PagedListModel(total, offset, limit, leagues);
        await handler.SetupGetJsonResponseAsync("http://api/leagues", result, options);
    }

    public static async Task SetupRefreshLeagues(
        this FakeHttpMessageHandler handler,
        ConfigureResponseOptions? options = null)
    {
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://api/leagues/refresh"),
            new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
            options
        );
    }

    public static async Task SetupFavoriteLeague(
        this FakeHttpMessageHandler handler,
        Guid id,
        ConfigureResponseOptions? options = null)
    {
        var statusCode = options?.Status ?? System.Net.HttpStatusCode.NoContent;
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/leagues/{id}/favorite"),
            new HttpResponseMessage(statusCode),
            options
        );
    }

    public static async Task SetupUnfavoriteLeague(
        this FakeHttpMessageHandler handler,
        Guid id,
        ConfigureResponseOptions? options = null)
    {
        var statusCode = options?.Status ?? System.Net.HttpStatusCode.NoContent;
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/leagues/{id}/unfavorite"),
            new HttpResponseMessage(statusCode),
            options
        );
    }
}