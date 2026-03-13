namespace Personal.Dashboard.Models;

public record FootballLeagueSeasonModel(int Year, bool IsCurrent);

public record FootballLeagueModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballLeagueSeasonModel[] Seasons);

public record FootballClubLeagueModel(Guid Id, string Name);

public record FootballClubModel(
    Guid Id,
    string Name,
    DateTimeOffset? LastRefreshed,
    bool IsFavorite,
    FootballClubLeagueModel[] Leagues);
