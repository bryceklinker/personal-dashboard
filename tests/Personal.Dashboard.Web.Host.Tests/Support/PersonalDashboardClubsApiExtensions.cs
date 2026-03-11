using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public static class PersonalDashboardClubsApiExtensions
{
    public static async Task SetupClubs(
        this FakeHttpMessageHandler handler,
        long offset = 0,
        long limit = 10,
        long total = 10,
        ConfigureResponseOptions? options = null,
        params FootballClubModel[] clubs
    )
    {
        var result = DataFactory.PagedListModel(total, offset, limit, clubs);
        await handler.SetupGetJsonResponseAsync("http://api/clubs", result, options);
    }

    public static async Task SetupRefreshClubs(
        this FakeHttpMessageHandler handler,
        ConfigureResponseOptions? options = null)
    {
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://api/clubs/refresh"),
            new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
            options
        );
    }

    public static async Task SetupFavoriteClub(
        this FakeHttpMessageHandler handler,
        Guid id,
        ConfigureResponseOptions? options = null)
    {
        var statusCode = options?.Status ?? System.Net.HttpStatusCode.NoContent;
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/clubs/{id}/favorite"),
            new HttpResponseMessage(statusCode),
            options
        );
    }

    public static async Task SetupUnfavoriteClub(
        this FakeHttpMessageHandler handler,
        Guid id,
        ConfigureResponseOptions? options = null)
    {
        var statusCode = options?.Status ?? System.Net.HttpStatusCode.NoContent;
        await handler.SetupResponseAsync(
            new HttpRequestMessage(HttpMethod.Post, $"http://api/clubs/{id}/unfavorite"),
            new HttpResponseMessage(statusCode),
            options
        );
    }
}
