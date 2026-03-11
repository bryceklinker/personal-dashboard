using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class FavoriteClubCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public FavoriteClubCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenFavoriteClubCommandExecutedThenClubIsFavorite()
    {
        var club = PersonalDashboardEntityFactory.FootballClub();
        _context.Add(club);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new FavoriteClubCommand(club.Id));

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.True(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenFavoriteCalledForNonExistentClubThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new FavoriteClubCommand(Guid.NewGuid())));
    }
}
