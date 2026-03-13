using Bogus;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Test.Support;

public static class DataFactory
{
    private static readonly Faker Faker = new ();
    public static FootballLeagueModel FootballLeagueModel()
    {
        return new FootballLeagueModel(
            Faker.Random.Guid(),
            Faker.Company.CompanyName(),
            Faker.Date.RecentOffset(),
            Faker.Random.Bool(),
            [new FootballLeagueSeasonModel(Faker.Random.Int(2020, 2025), true)]
        );
    }

    public static FootballClubModel FootballClubModel()
    {
        return new FootballClubModel(
            Faker.Random.Guid(),
            Faker.Company.CompanyName(),
            Faker.Date.RecentOffset(),
            Faker.Random.Bool(),
            [new FootballClubLeagueModel(Faker.Random.Guid(), Faker.Company.CompanyName())]
        );
    }

    public static PagedListResultModel<T> PagedListModel<T>(
        long total = 0,
        long offset = 0,
        long limit = 10,
        T[]? items = null
    )
    {
        return new PagedListResultModel<T>(items ?? [], total, offset, limit);
    }

    public static PagedListResultModel<T> PagedListModelFromItems<T>(params T[] items)
    {
        return PagedListModel(items.Length, 0, items.Length, items);
    }
    
    public static T[] Many<T>(Func<T> factory, int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => factory())
            .ToArray();
    }
}