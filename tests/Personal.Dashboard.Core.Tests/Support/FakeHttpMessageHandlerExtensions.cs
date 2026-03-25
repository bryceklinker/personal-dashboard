using Personal.Dashboard.Core.Common.Apis.FootballApi;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Support;

public static class FootballApiHttpMessageHandlerExtensions
{
    public static async Task SetupGetLeagues(this FakeHttpMessageHandler handler, string baseUrl, FootballApiLeague[] leagues)
    {
        await handler.SetupGetJsonResponseAsync(
            $"{baseUrl}/leagues",
            FootballApiDataFactory.SuccessResponse(leagues)
        );
    }

    public static async Task SetupGetTeams(
        this FakeHttpMessageHandler handler,
        string baseUrl,
        long leagueId,
        int seasonYear,
        FootballApiTeam[] teams)
    {
        await handler.SetupGetJsonResponseAsync(
            $"{baseUrl}/teams?league={leagueId}&season={seasonYear}",
            FootballApiDataFactory.SuccessResponse(teams)
        );
    }
}