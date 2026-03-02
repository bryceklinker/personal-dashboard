using System.Web;

namespace Personal.Dashboard.Models;

public record ListResultModel<T>(T[] Items, long Total);

public record PagedListResultModel<T>(T[] Items, long Total, long Offset, long Limit)
    : ListResultModel<T>(Items, Total)
{
    public static PagedListResultModel<T> Empty()
    {
        return new PagedListResultModel<T>([], 0, 0, 0);
    }
}

public abstract record QueryParameters
{
    private readonly Dictionary<string, string> _parameters = new();

    public void Add(string key, string value)
    {
        _parameters.Add(key, value);
    }
    
    public string ToQueryString()
    {
        var pairs = _parameters.Select(k => $"{k.Key}={HttpUtility.UrlEncode(k.Value)}");
        return string.Join("&", pairs);
    }   
}

public record PagedListParameters : QueryParameters
{
    public PagedListParameters(long offset = 0, long limit = 10)
    {
        Add(nameof(offset), offset.ToString());
        Add(nameof(limit), limit.ToString());
    }
    
    public static PagedListParameters Default() => new();
}