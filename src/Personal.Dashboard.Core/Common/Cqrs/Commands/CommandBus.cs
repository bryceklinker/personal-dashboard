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
    Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}

public class CommandBus(IMediator mediator) : ICommandBus
{
    public async Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
    }

    public Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}