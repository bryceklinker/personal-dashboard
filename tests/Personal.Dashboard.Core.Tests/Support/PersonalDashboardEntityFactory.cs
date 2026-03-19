using Bogus;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Countries.Entities;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Tests.Support;

public static class PersonalDashboardEntityFactory
{
    private static readonly Faker Faker = new();

    public static FootballCountryEntity FootballCountry(Action<FootballCountryEntity>? configure = null)
    {
        var entity = new FootballCountryEntity
        {
            Name = Faker.Address.Country(),
            Code = Faker.Address.CountryCode(),
            Flag = Faker.Internet.Url(),
        };
        configure?.Invoke(entity);
        return entity;
    }

    public static FootballLeagueEntity FootballLeague(Action<FootballLeagueEntity>? configure = null)
    {
        var entity = new FootballLeagueEntity
        {
            Name = Faker.Company.CompanyName(),
            Id = Faker.Random.Guid(),
            Country = FootballCountry(),
        };
        configure?.Invoke(entity);
        return entity;
    }

    public static FootballClubEntity FootballClub(Action<FootballClubEntity>? configure = null)
    {
        var entity = new FootballClubEntity
        {
            Id = Faker.Random.Guid(),
            Name = Faker.Company.CompanyName(),
        };
        configure?.Invoke(entity);
        return entity;
    }

    public static (FootballLeagueEntity League, FootballClubEntity Club) FootballClubInLeague(
        string leagueApiAlias = "123",
        string clubApiAlias = "456")
    {
        var league = FootballLeague(l => l.AddAlias(DataSource.FootballApi, leagueApiAlias));
        var club = FootballClub();
        club.AddAlias(DataSource.FootballApi, clubApiAlias);
        club.AddLeague(league);
        return (league, club);
    }
}