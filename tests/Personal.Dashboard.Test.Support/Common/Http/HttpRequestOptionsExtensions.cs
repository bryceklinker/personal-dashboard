namespace Personal.Dashboard.Test.Support.Common.Http;

internal static class HttpRequestOptionsExtensions
{
    public static void CloneTo(this HttpRequestOptions source, HttpRequestOptions target)
    {
        foreach (var option in source)
        {
            target.TryAdd(option.Key, option.Value);
        }
    }
}