namespace Personal.Dashboard.Test.Support.Common.Http;

internal static class HttpRequestMessageExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy =  request.VersionPolicy,
            Content = request.Content is null 
                ? null 
                : await request.Content.CloneAsync() 
        };
        request.Headers.CloneTo(clone.Headers);
        request.Options.CloneTo(clone.Options);
        return clone;
    }
}