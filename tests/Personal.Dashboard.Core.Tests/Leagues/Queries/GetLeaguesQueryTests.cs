using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Queries;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Core.Tests.Leagues.Queries;

public class GetLeaguesQueryTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public GetLeaguesQueryTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenNoLeaguesExistThenReturnsEmptyResult()
    {
        var result = await _bus.QueryAsync(new GetLeaguesQuery());
        
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Offset);
        Assert.Equal(10, result.Limit);
    }

    [Fact]
    public async Task WhenLeaguesExistThenReturnsADefaultOfTenLeagues()
    {
        _context.AddMany(DataFactory.Many(() => PersonalDashboardEntityFactory.FootballLeague(), 50));
        await _context.SaveChangesAsync();

        var result = await _bus.QueryAsync(new GetLeaguesQuery());
        Assert.Equal(50, result.Total);
        Assert.Equal(0, result.Offset);
        Assert.Equal(10, result.Limit);
        Assert.Equal(10, result.Items.Length);
    }

    [Fact]
    public async Task WhenLeagueIsFavoriteThenReturnedModelHasIsFavoriteTrue()
    {
        var league = PersonalDashboardEntityFactory.FootballLeague(l => l.Favorite());
        _context.Add(league);
        await _context.SaveChangesAsync();

        var result = await _bus.QueryAsync(new GetLeaguesQuery());
        Assert.True(result.Items[0].IsFavorite);
    }
}