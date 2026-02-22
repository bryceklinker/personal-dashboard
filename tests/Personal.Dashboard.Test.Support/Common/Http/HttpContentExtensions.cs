namespace Personal.Dashboard.Test.Support.Common.Http;

internal static class HttpContentExtensions
{
    public static async Task<HttpContent> CloneAsync(this HttpContent content)
    {
        var clone = new ByteArrayContent(await content.ReadAsByteArrayAsync());
        content.Headers.CloneTo(clone.Headers);
        return clone;
    }
}