using System.Net;

namespace Personal.Dashboard.Test.Support.Common.Http;

public record ConfiguredHttpRequest
{
    private readonly FakeHttpRequest _request;
    private readonly FakeHttpResponse _response;
    private readonly ConfigureResponseOptions? _options;
    
    private Action<HttpRequestMessage> Capture => _options?.Capture ?? (_ => { });
    private Func<HttpRequestMessage, Task> CaptureAsync => _options?.CaptureAsync ?? (_ => Task.CompletedTask);
    
    private ConfiguredHttpRequest(
        FakeHttpRequest request, 
        FakeHttpResponse response,
        ConfigureResponseOptions? options)
    {
        _request = request;
        _response = response;
        _options = options;
    }
    
    public async Task<HttpResponseMessage?> ToResponseAsync(HttpRequestMessage request)
    {
        var matches = await _request.Matches(request);
        if (!matches)
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        
        Capture(await request.CloneAsync());
        await CaptureAsync(await request.CloneAsync());
        return await _response.ToResponseAsync();
    }

    public static async Task<ConfiguredHttpRequest> From(
        HttpRequestMessage request, 
        HttpResponseMessage response,
        ConfigureResponseOptions? options)
    {
        ArgumentNullException.ThrowIfNull(request.RequestUri);

        var fakeRequest = await FakeHttpRequest.FromRequestAsync(request);
        var responseClone = await FakeHttpResponse.FromResponseAsync(response);
        return new ConfiguredHttpRequest(fakeRequest, responseClone, options);
    }
}