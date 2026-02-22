using System.Collections.Specialized;

namespace Personal.Dashboard.Core.Common.Http;

public static class UriExtensions
{
    public static NameValueCollection ParseQueryString(this Uri? uri)
    {
        return uri is null
            ? new NameValueCollection()
            : uri.Query.ParseAsQueryString();
    }
}