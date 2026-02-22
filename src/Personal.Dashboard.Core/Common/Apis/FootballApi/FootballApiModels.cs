namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public record FootballApiPaging(long Current, long Total);

public record FootballApiCountry(string Name, string Code, string Flag);

public record FootballApiLeagueInfo(long Id, string Name, string Type, string Logo);

public record FootballApiFixtureCoverage(
    bool Events,
    bool Lineups,
    bool Statistics_Fixtures,
    bool Statistics_Players
);

public record FootballApiSeasonCoverage(
    bool Standings,
    bool Players,
    bool Top_Scorers,
    bool Top_Assists,
    bool Top_Cards,
    bool Injuries,
    bool Predictions,
    bool Odds,
    FootballApiFixtureCoverage Fixtures
);

public record FootballApiSeason(
    long Year,
    DateOnly Start,
    DateOnly End,
    bool Current,
    FootballApiSeasonCoverage Coverage);

public record FootballApiLeague(
    FootballApiCountry Country,
    FootballApiLeagueInfo League,
    FootballApiSeason[] Seasons
);

public record FootballApiLeaguesParameters(
    long? Id = null,
    string? Name = null,
    string? Country = null,
    string? Code = null,
    long? Season = null,
    long? Team = null,
    string? Type = null,
    bool? Current = null,
    string? Search = null,
    string? Last = null
)
{
    public static FootballApiLeaguesParameters Empty() => new();
};

public record FootballApiError(
    DateTimeOffset Time,
    string Bug,
    string Report
);

public record FootballApiResponse<
    TParameters,
    TResponse
>(
    string Get,
    TParameters Parameters,
    FootballApiError[] Errors,
    long Results,
    FootballApiPaging Paging,
    TResponse Response
) where TParameters : class;

public record FootballApiLeaguesResponse(
    FootballApiLeaguesParameters Parameters,
    FootballApiError[] Errors,
    long Results,
    FootballApiPaging Paging,
    FootballApiLeague[] Response
    )
    : FootballApiResponse<FootballApiLeaguesParameters, FootballApiLeague[]>("leagues", Parameters, Errors, Results, Paging, Response);