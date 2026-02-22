using System.Collections.Specialized;

namespace Personal.Dashboard.Core.Common.Http;

public static class HttpRequestMessageExtensions
{
    public static NameValueCollection ParseQueryString(this HttpRequestMessage? request)
    {
        return request is null
            ? new NameValueCollection()
            : request.RequestUri.ParseQueryString();
    }
}