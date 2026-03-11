using AutoMapper;
using AutoMapper.QueryableExtensions;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Queries;

public record GetClubsQuery(int Offset = 0, int Limit = 10) : PagedQuery<FootballClubModel>(Offset, Limit);

public class GetClubsQueryHandler(
    PersonalDashboardContext context,
    IMapper mapper
) : IQueryHandler<GetClubsQuery, PagedListResultModel<FootballClubModel>>
{
    public async Task<PagedListResultModel<FootballClubModel>> Handle(GetClubsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Set<FootballClubEntity>()
            .ProjectTo<FootballClubModel>(mapper.ConfigurationProvider);
        return await query.ToPagedListAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
