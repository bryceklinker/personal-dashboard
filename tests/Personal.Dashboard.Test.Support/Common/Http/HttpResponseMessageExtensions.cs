namespace Personal.Dashboard.Test.Support.Common.Http;

internal static class HttpResponseMessageExtensions
{
    public static async Task<HttpResponseMessage> CloneAsync(this HttpResponseMessage response)
    {
        var clone = new HttpResponseMessage(response.StatusCode)
        {
            Content = await response.Content.CloneAsync(),
            ReasonPhrase = response.ReasonPhrase,
            Version = response.Version,
            RequestMessage = response.RequestMessage is null
                ? null
                : await response.RequestMessage.CloneAsync(),
        };
        response.Headers.CloneTo(clone.Headers);
        response.TrailingHeaders.CloneTo(clone.TrailingHeaders);
        return response;
    }
}