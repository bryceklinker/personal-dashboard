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
}