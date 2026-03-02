using System.Net.Http.Headers;

namespace Personal.Dashboard.Test.Support.Common.Http;

public class FakeHttpRequest
{
    private HttpMethod Method { get; }
    private Uri? RequestUri { get; }
    public HttpHeaders RequestHeaders { get; }
    public HttpContentHeaders? ContentHeaders { get; }
    public byte[] Content { get; }

    private FakeHttpRequest(
        HttpMethod method, 
        Uri? requestUri,
        HttpHeaders requestHeaders,
        HttpContentHeaders? contentHeaders,
        byte[] content)
    {
        Method = method;
        RequestUri = requestUri;
        RequestHeaders = requestHeaders;
        ContentHeaders = contentHeaders;
        Content = content;
    }
    
    public static async Task<FakeHttpRequest> FromRequestAsync(HttpRequestMessage request)
    {
        var content = request.Content is null 
            ? []
            : await request.Content.ReadAsByteArrayAsync();
        return new FakeHttpRequest(
            request.Method,
            request.RequestUri,
            request.Headers,
            request.Content?.Headers,
            content);
    }

    public async Task<bool> Matches(HttpRequestMessage request)
    {
        if (Method != request.Method)
            return false;
        
        return RequestUri?.LocalPath == request.RequestUri?.LocalPath;
    }
}