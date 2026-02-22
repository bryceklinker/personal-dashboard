using Personal.Dashboard.Core.Common.Cqrs.Commands;

namespace Personal.Dashboard.Core.Common.Cqrs;

public interface ICqrsBus :  ICommandBus
{
    
}

public class CqrsBus(ICommandBus commandBus) : ICqrsBus
{
    public async Task ExecuteAsync(ICommand command)
    {
        await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        return await commandBus.ExecuteAsync(command).ConfigureAwait(false);
    }
}