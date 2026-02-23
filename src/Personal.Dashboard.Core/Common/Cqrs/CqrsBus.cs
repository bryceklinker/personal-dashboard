using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Queries;

namespace Personal.Dashboard.Core.Common.Cqrs;

public interface ICqrsBus : ICommandBus, IQueryBus
{
}

public class CqrsBus(
    ICommandBus commandBus,
    IQueryBus queryBus
) : ICqrsBus
{
    public async Task ExecuteAsync(ICommand command)
    {
        await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        return await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        return await queryBus.QueryAsync(query).ConfigureAwait(false);
    }
}