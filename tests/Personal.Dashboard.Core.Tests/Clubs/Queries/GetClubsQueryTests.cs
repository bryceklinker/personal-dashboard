using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Queries;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Queries;

public class GetClubsQueryTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public GetClubsQueryTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenNoClubsExistThenReturnsEmptyResult()
    {
        var result = await _bus.QueryAsync(new GetClubsQuery());
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task WhenClubsExistThenReturnsMappedClubs()
    {
        _context.AddMany(DataFactory.Many(() => PersonalDashboardEntityFactory.FootballClub(), 15));
        await _context.SaveChangesAsync();

        var result = await _bus.QueryAsync(new GetClubsQuery());
        Assert.Equal(15, result.Total);
        Assert.Equal(10, result.Items.Length);
    }
}
