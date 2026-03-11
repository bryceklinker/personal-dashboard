using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record FavoriteClubCommand(Guid ClubId) : ICommand;

public class FavoriteClubCommandHandler(PersonalDashboardContext context) : ICommandHandler<FavoriteClubCommand>
{
    public async Task Handle(FavoriteClubCommand request, CancellationToken cancellationToken)
    {
        var club = await context.Set<FootballClubEntity>().FindAsync([request.ClubId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballClubEntity), request.ClubId);
        club.Favorite();
        await context.SaveChangesAsync(cancellationToken);
    }
}
