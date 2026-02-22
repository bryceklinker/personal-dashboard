namespace Personal.Dashboard.Test.Support.Common.Http;

public class FakeHttpClientFactory(FakeHttpMessageHandler handler) : IHttpClientFactory
{
    
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(handler);
    }
}