using System.Net;
using System.Net.Http.Headers;

namespace Personal.Dashboard.Test.Support.Common.Http;

public class FakeHttpResponse
{
    private HttpStatusCode StatusCode { get; }
    private HttpResponseHeaders Headers { get; }
    private HttpContentHeaders ContentHeaders { get; }
    private byte[] Content { get; }

    private FakeHttpResponse(
        HttpStatusCode statusCode,
        HttpResponseHeaders headers,
        HttpContentHeaders contentHeaders,
        byte[] content
        )
    {
        StatusCode = statusCode;
        Headers = headers;
        ContentHeaders = contentHeaders;
        Content = content;
    }
    
    public Task<HttpResponseMessage> ToResponseAsync()
    {
        var response = new HttpResponseMessage(StatusCode)
        {
            Content = new ByteArrayContent(Content)
        };
        Headers.CloneTo(response.Headers);
        ContentHeaders.CloneTo(response.Content.Headers);
        return Task.FromResult(response);
    }
    
    public static async Task<FakeHttpResponse> FromResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsByteArrayAsync();
        return new FakeHttpResponse(response.StatusCode, response.Headers, response.Content.Headers, content);
    }
}