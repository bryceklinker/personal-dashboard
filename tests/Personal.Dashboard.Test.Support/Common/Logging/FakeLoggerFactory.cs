using Microsoft.Extensions.Logging;

namespace Personal.Dashboard.Test.Support.Common.Logging;

public class FakeLoggerFactory(FakeLogger logger) : ILoggerFactory
{
    public void Dispose()
    {
        
    }

    public ILogger CreateLogger(string categoryName)
    {
        return logger;
    }

    public void AddProvider(ILoggerProvider provider)
    {
        
    }
}