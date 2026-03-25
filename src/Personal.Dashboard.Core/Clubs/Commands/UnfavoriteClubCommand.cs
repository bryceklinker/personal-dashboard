using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Exceptions;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Core.Clubs.Commands;

public record UnfavoriteClubCommand(Guid ClubId) : ICommand;

public class UnfavoriteClubCommandHandler(PersonalDashboardContext context) : ICommandHandler<UnfavoriteClubCommand>
{
    public async Task Handle(UnfavoriteClubCommand request, CancellationToken cancellationToken)
    {
        var club = await context.Set<FootballClubEntity>().FindAsync([request.ClubId], cancellationToken)
            ?? throw new EntityNotFoundException(typeof(FootballClubEntity), request.ClubId);
        club.Unfavorite();
        await context.SaveChangesAsync(cancellationToken);
    }
}
