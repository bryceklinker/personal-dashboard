namespace Personal.Dashboard.Models;

public record FootballLeagueSeasonModel(int Year, bool IsCurrent);

public record FootballCountryModel(string Name, string? Code, string? Flag);

public record FootballLeagueModel(
    Guid Id,
    string Name,
    FootballCountryModel? Country,
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
