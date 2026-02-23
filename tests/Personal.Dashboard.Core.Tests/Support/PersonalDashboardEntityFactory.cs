using Bogus;
using Personal.Dashboard.Core.Leagues.Entities;

namespace Personal.Dashboard.Core.Tests.Support;

public static class PersonalDashboardEntityFactory
{
    private static readonly Faker Faker = new();
    
    public static FootballLeagueEntity FootballLeague(Action<FootballLeagueEntity>? configure = null)
    {
        var entity = new FootballLeagueEntity
        {
            Name = Faker.Company.CompanyName(),
            Id = Faker.Random.Guid(),
        };
        configure?.Invoke(entity);
        return entity;
    }
}