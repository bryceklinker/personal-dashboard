using System.Text.Json.Serialization;

namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public record FootballApiPaging(long Current, long Total);

public record FootballApiCountry(string Name, string Code, string Flag);

public record FootballApiLeagueInfo(long Id, string Name, string Type, string Logo);

public abstract record FootballApiParameters(IDictionary<string, object?> Parameters)
{
    public object? this[string key]
    {
        get => Parameters.TryGetValue(key, out var value) ? value : null;
        set => Parameters[key] = value;
    }
    
    public string AsQueryString()
    {
        var defined = Parameters
            .Where(p => p.Value is not null)
            .ToArray();
        if (defined.Length == 0)
            return "";
        
        var parts = defined
            .Select(p => $"{p.Key}={p.Value}".ToLowerInvariant());
        
        return string.Join("&", parts);
    }
}

public record FootballApiFixtureCoverage(
    bool Events,
    bool Lineups,
    bool Statistics_Fixtures,
    bool Statistics_Players
)
{
    [JsonPropertyName("statistics_fixtures")]
    public bool Statistics_Fixtures { get; init; }
    
    [JsonPropertyName("statistics_players")]
    public bool Statistics_Players { get; init; }
};

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
)
{
    [JsonPropertyName("top_scorers")]
    public bool Top_Scorers { get; init; }
    
    [JsonPropertyName("top_assists")]
    public bool Top_Assists { get; init; }
    
    [JsonPropertyName("top_cards")]
    public bool Top_Cards { get; init; }
};

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
) : FootballApiParameters(new Dictionary<string, object?>
{
    { "id", Id },
    { "name", Name },
    { "country", Country },
    { "code", Code },
    { "season", Season },
    { "team", Team },
    { "type", Type },
    { "current", Current },
    { "search", Search },
    { "last", Last }
})
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