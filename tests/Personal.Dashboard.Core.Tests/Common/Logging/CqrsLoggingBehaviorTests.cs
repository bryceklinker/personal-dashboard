using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Logging;

namespace Personal.Dashboard.Core.Tests.Common.Logging;

public class CqrsLoggingBehaviorTests
{
    private readonly FakeLogger _logger;
    private readonly ICqrsBus _cqrsBus;

    public CqrsLoggingBehaviorTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(opts =>
        {
            opts.AddAssembly(typeof(CqrsLoggingBehaviorTests).Assembly);
        });
        
        _logger = provider.GetRequiredService<FakeLogger>();
        _cqrsBus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenCommandIsExecutedThenLogsStartOfCommand()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _cqrsBus.ExecuteAsync(new FailingCommand()));
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Single(logItems);
        Assert.StartsWith("Starting command", logItems[0].Message);
    }

    [Fact]
    public async Task WhenCommandIsExecutedThenLogsCommandFinished()
    {
        await _cqrsBus.ExecuteAsync(new SuccessCommand());
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Equal(2, logItems.Length);
        Assert.StartsWith("Finished command", logItems[0].Message);
    }

    [Fact]
    public async Task WhenCommandIsExecutedThenLogsEndOfCommand()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _cqrsBus.ExecuteAsync(new FailingCommand()));
        var logItems = _logger.GetLogItems(LogLevel.Error);
        Assert.Single(logItems);
        Assert.StartsWith("Failed command", logItems[0].Message);
    }

    [Fact]
    public async Task WhenQueryIsExecutedThenLogsStartOfQuery()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _cqrsBus.QueryAsync(new FailingQuery()));
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Single(logItems);
        Assert.StartsWith("Starting query", logItems[0].Message);
    }
    
    [Fact]
    public async Task WhenQueryIsExecutedThenLogsEndOfQuery()
    {
        await _cqrsBus.QueryAsync(new SuccessQuery());
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Equal(2, logItems.Length);
        Assert.StartsWith("Finished query", logItems[0].Message);
    }
    
    [Fact]
    public async Task WhenQueryIsExecutedThenLogsQueryFailure()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _cqrsBus.QueryAsync(new FailingQuery()));
        var logItems = _logger.GetLogItems(LogLevel.Error);
        Assert.Single(logItems);
        Assert.StartsWith("Failed query", logItems[0].Message);
    }
}

public record FailingCommand : ICommand;

public class FailingCommandHandler : IRequestHandler<FailingCommand>
{
    public Task Handle(FailingCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public record SuccessCommand : ICommand;

public class SuccessCommandHandler : IRequestHandler<SuccessCommand>
{
    public Task Handle(SuccessCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public record FailingQuery : IQuery<object>;

public class FailingQueryHandler : IQueryHandler<FailingQuery, object>
{
    public Task<object> Handle(FailingQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public record SuccessQuery : IQuery<object>;

public class SuccessQueryHandler : IQueryHandler<SuccessQuery, object>
{
    public Task<object> Handle(SuccessQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new { });
    }
} 