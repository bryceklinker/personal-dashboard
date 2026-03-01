using System.Collections.Specialized;
using System.Web;

namespace Personal.Dashboard.Test.Support.Common.Http;

public static class StringExtensions
{
    public static NameValueCollection ParseAsQueryString(this string? queryString)
    {
        return queryString is null
            ?  new NameValueCollection() 
            : HttpUtility.ParseQueryString(queryString);
    }
}