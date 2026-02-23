using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Common.Storage;

public static class QueryableExtensions
{
    public static async Task<PagedListResultModel<T>> ToPagedListAsync<T>(
        this IQueryable<T> source,
        int offset = 0,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var total = await source.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await source
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedListResultModel<T>(items, total, offset, limit);
    }

    public static async Task<PagedListResultModel<T>> ToPagedListAsync<T>(
        this IQueryable<T> source,
        IPagedQuery<T> query,
        CancellationToken cancellationToken = default)
    {
        return await source.ToPagedListAsync(query.Offset, query.Limit, cancellationToken)
            .ConfigureAwait(false);
    }
}