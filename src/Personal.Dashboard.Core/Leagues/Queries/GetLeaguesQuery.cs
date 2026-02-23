using AutoMapper;
using AutoMapper.QueryableExtensions;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Leagues.Queries;

public record GetLeaguesQuery(int Offset = 0, int Limit = 10) : PagedQuery<FootballLeagueModel>(Offset, Limit);

public class GetLeaguesQueryHandler(
    PersonalDashboardContext context,
    IMapper mapper
) : IQueryHandler<GetLeaguesQuery, PagedListResultModel<FootballLeagueModel>>
{
    public async Task<PagedListResultModel<FootballLeagueModel>> Handle(GetLeaguesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Set<FootballLeagueEntity>()
            .ProjectTo<FootballLeagueModel>(mapper.ConfigurationProvider);

        return await query.ToPagedListAsync(request, cancellationToken).ConfigureAwait(false);
    }
}