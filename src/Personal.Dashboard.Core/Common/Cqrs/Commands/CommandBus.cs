using MediatR;

namespace Personal.Dashboard.Core.Common.Cqrs.Commands;

public interface ICommand : IRequest;

public interface ICommand<out TResult> : IRequest<TResult>;

public interface ICommandBus
{
    Task ExecuteAsync(ICommand command);

    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command);
}

public class CommandBus(IMediator mediator) : ICommandBus
{
    public async Task ExecuteAsync(ICommand command)
    {
        await mediator.Send(command).ConfigureAwait(false);
    }

    public Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        throw new NotImplementedException();
    }
}