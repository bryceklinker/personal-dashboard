using MediatR;

namespace Personal.Dashboard.Core.Common.Cqrs.Commands;

public interface ICommand : IRequest;
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand> 
    where TCommand : ICommand;

public interface ICommand<out TResult> : ICommand, IRequest<TResult>;

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;

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