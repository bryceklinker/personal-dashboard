using System.Collections;
using Bogus;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core.Tests.Support;

public static class FootballApiDataFactory
{
    private static readonly Faker Faker = new();

    public static FootballApiError Error()
    {
        return new FootballApiError(
            Faker.Date.RecentOffset(),
            Faker.Hacker.Phrase(),
            Faker.Rant.Review()
        );
    }

    public static FootballApiPaging Paging()
    {
        return new FootballApiPaging(
            1,
            1
        );
    }

    public static FootballApiLeagueInfo LeagueInfo()
    {
        return new FootballApiLeagueInfo(
            Faker.Random.Long(),
            Faker.Company.CompanyName(),
            Faker.PickRandom("league", "cup"),
            Faker.Internet.Url()
        );
    }

    public static FootballApiCountry Country()
    {
        return new FootballApiCountry(
            Faker.Address.Country(),
            Faker.Address.CountryCode(),
            Faker.Internet.Url()
        );
    }

    public static FootballApiFixtureCoverage FixtureCoverage()
    {
        return new FootballApiFixtureCoverage(false, false, false, false);
    }

    public static FootballApiSeasonCoverage SeasonCoverage()
    {
        return new FootballApiSeasonCoverage(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            FixtureCoverage()
        );
    }

    public static FootballApiSeason Season()
    {
        var start = Faker.Date.PastDateOnly();
        return new FootballApiSeason(
            Faker.Date.RecentDateOnly().Year,
            start,
            start.AddMonths(9),
            true,
            SeasonCoverage()
        );
    }

    public static FootballApiLeague League()
    {
        return new FootballApiLeague(
            Country(),
            LeagueInfo(),
            [Season()]
        );
    }

    public static FootballApiResponse<TParameters, TResponse> SuccessResponse<TParameters, TResponse>(
        TParameters parameters,
        TResponse response
    )
        where TParameters : class
    {
        return new FootballApiResponse<TParameters, TResponse>(
            Faker.Random.AlphaNumeric(8),
            parameters,
            [],
            response is IEnumerable enumerable ? enumerable.Cast<object>().Count() : 1,
            Paging(),
            response
        );
    }

    public static FootballApiResponse<TParameters, TResponse> FailureResponse<TParameters, TResponse>(
        TParameters parameters,
        TResponse response,
        FootballApiError[] errors
    )
        where TParameters : class
    {
        return new FootballApiResponse<TParameters, TResponse>(
            Faker.Random.AlphaNumeric(8),
            parameters,
            errors,
            response is IEnumerable enumerable ? enumerable.Cast<object>().Count() : 1,
            Paging(),
            response
        );
    }
}