using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Events;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Logging;

namespace Personal.Dashboard.Core.Tests.Common.Logging;

public class CqrsEventLoggingBehaviorTests
{
    private readonly FakeLogger _logger;
    private readonly ICqrsBus _cqrsBus;

    public CqrsEventLoggingBehaviorTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(opts =>
        {
            opts.AddAssembly(typeof(CqrsEventLoggingBehaviorTests).Assembly);
        });

        _logger = provider.GetRequiredService<FakeLogger>();
        _cqrsBus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenEventIsPublishedThenLogsPublishingEvent()
    {
        await _cqrsBus.PublishAsync(new SuccessEvent());
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Contains(logItems, l => l.Message.StartsWith("Publishing event"));
    }

    [Fact]
    public async Task WhenEventIsPublishedThenLogsPublishedEvent()
    {
        await _cqrsBus.PublishAsync(new SuccessEvent());
        var logItems = _logger.GetLogItems(LogLevel.Information);
        Assert.Contains(logItems, l => l.Message.StartsWith("Published event"));
    }

    [Fact]
    public async Task WhenEventFailsThenLogsError()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _cqrsBus.PublishAsync(new FailingEvent()));
        var logItems = _logger.GetLogItems(LogLevel.Error);
        Assert.Single(logItems);
        Assert.StartsWith("Failed event", logItems[0].Message);
    }
}

public record SuccessEvent : IEvent;

public class SuccessEventHandler : IEventHandler<SuccessEvent>
{
    public Task Handle(SuccessEvent notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public record FailingEvent : IEvent;

public class FailingEventHandler : IEventHandler<FailingEvent>
{
    public Task Handle(FailingEvent notification, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
