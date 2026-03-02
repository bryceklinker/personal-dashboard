namespace Personal.Dashboard.Test.Support;

public record EventuallyOptions(TimeSpan WaitTime, TimeSpan Delay)
{
    public static EventuallyOptions Default => new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
}

public static class Eventually
{
    public static async Task Assert(Action assertion, EventuallyOptions? options = null)
    {
        await AssertAsync(() =>
        {
            assertion();
            return Task.CompletedTask;
        }, options);
    }

    public static async Task AssertAsync(Func<Task> assertion, EventuallyOptions? options = null)
    {
        var opts = options ?? EventuallyOptions.Default;
        var endTime = DateTimeOffset.UtcNow.Add(opts.WaitTime);
        Exception? exception;
        do
        {
            try
            {
                await assertion();
                return;
            }
            catch (Exception e)
            {
                exception = e;
                await Task.Delay(opts.Delay);
            }
        } while(DateTimeOffset.UtcNow < endTime);
        
        if (exception != null)
            throw exception;
    }
}