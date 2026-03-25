using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Clubs.Commands;

public class UnfavoriteClubCommandTests
{
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _bus;

    public UnfavoriteClubCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create();
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _bus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenUnfavoriteClubCommandExecutedThenClubIsNotFavorite()
    {
        var club = PersonalDashboardEntityFactory.FootballClub(c => c.Favorite());
        _context.Add(club);
        await _context.SaveChangesAsync();

        await _bus.ExecuteAsync(new UnfavoriteClubCommand(club.Id));

        var updated = await _context.Set<FootballClubEntity>().FindAsync(club.Id);
        Assert.False(updated?.IsFavorite);
    }

    [Fact]
    public async Task WhenUnfavoriteCalledForNonExistentClubThenThrowsEntityNotFoundException()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _bus.ExecuteAsync(new UnfavoriteClubCommand(Guid.NewGuid())));
    }
}
