using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;

namespace Personal.Dashboard.Test.Support.Common.Http;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentBag<ConfiguredHttpRequest> _requests = [];
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tasks = _requests.ToArray()
            .Select(r => r.ToResponseAsync(request));
        var responses = await Task.WhenAll(tasks);
        var response = responses.FirstOrDefault(r => r is not null);
        return response ?? new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    public async Task SetupResponseAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response,
        ConfigureResponseOptions? options = null)
    {
        _requests.Add(await ConfiguredHttpRequest.From(request, response, options));
    }
    
    public async Task SetupGetJsonResponseAsync<T>(string url, T data, ConfigureResponseOptions? options = null)
    {
        var statusCode = options?.Status ?? HttpStatusCode.OK;
        await SetupResponseAsync(new HttpRequestMessage(HttpMethod.Get, url), new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(data, new MediaTypeHeaderValue(MediaTypeNames.Application.Json),
                JsonSerializerOptions.Web)
        }, options);
    }
}