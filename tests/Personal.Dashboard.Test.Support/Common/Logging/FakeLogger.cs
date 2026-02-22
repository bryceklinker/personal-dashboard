using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Personal.Dashboard.Test.Support.Common.Logging;

public record CapturedLogItem(LogLevel LogLevel, EventId EventId, object State, Exception? Exception, string Message);

public class FakeLogger : ILogger, IDisposable
{
    private readonly ConcurrentBag<CapturedLogItem> _logItems = [];

    public CapturedLogItem[] GetLogItems(LogLevel level)
    {
        return _logItems.Where(l => l.LogLevel == level).ToArray();
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logItems.Add(new CapturedLogItem(logLevel, eventId, state, exception, formatter(state, exception)));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }

    public void Dispose()
    {
        
    }
}