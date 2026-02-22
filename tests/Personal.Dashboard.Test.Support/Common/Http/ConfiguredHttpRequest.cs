namespace Personal.Dashboard.Test.Support.Common.Http;

public record ConfiguredHttpRequest
{
    private readonly HttpRequestMessage _request;
    private readonly HttpResponseMessage _response;
    private readonly ConfigureResponseOptions? _options;

    private Action<HttpRequestMessage> Capture => _options?.Capture ?? (_ => { });
    private Func<HttpRequestMessage, Task> CaptureAsync => _options?.CaptureAsync ?? (_ => Task.CompletedTask);
    
    private ConfiguredHttpRequest(
        HttpRequestMessage request, 
        HttpResponseMessage response,
        ConfigureResponseOptions? options)
    {
        _request = request;
        _response = response;
        _options = options;
    }
    
    public async Task<HttpResponseMessage?> ToResponseAsync(HttpRequestMessage request)
    {
        if (_request.Method != request.Method)
            return null;
        
        if (_request.RequestUri?.LocalPath != request.RequestUri?.LocalPath)
            return null;

        Capture(await request.CloneAsync());
        await CaptureAsync(await request.CloneAsync());
        return await _response.CloneAsync();
    }

    public static async Task<ConfiguredHttpRequest> From(
        HttpRequestMessage request, 
        HttpResponseMessage response,
        ConfigureResponseOptions? options)
    {
        ArgumentNullException.ThrowIfNull(request.RequestUri);
        
        var requestClone = await request.CloneAsync();
        var responseClone = await response.CloneAsync();
        return new ConfiguredHttpRequest(requestClone, responseClone, options);
    }
}