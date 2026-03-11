namespace Personal.Dashboard.Models;

public record FootballLeagueModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite = false
);

public record FootballClubModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite = false
);
