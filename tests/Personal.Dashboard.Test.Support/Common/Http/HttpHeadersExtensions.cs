using System.Net.Http.Headers;

namespace Personal.Dashboard.Test.Support.Common.Http;

internal static class HttpHeadersExtensions
{
    public static void CloneTo(this HttpHeaders source, HttpHeaders target)
    {
        foreach (var header in source)
            target.Add(header.Key, header.Value);
    }
}