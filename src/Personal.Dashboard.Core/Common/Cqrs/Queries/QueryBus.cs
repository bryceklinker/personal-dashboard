using MediatR;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Common.Cqrs.Queries;

public interface IQuery<out TResult> : IRequest<TResult>;

public interface IPagedQuery<TResult> : IQuery<PagedListResultModel<TResult>>
{
    int Offset { get; }
    int Limit { get; }
}

public abstract record PagedQuery<T>(int Offset, int Limit) : IPagedQuery<T>;

public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;

public interface IQueryBus
{
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
}

public class QueryBus(IMediator mediator) : IQueryBus
{
    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        return await mediator.Send(query).ConfigureAwait(false);
    }
}