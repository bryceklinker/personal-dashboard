using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public interface IFootballApiClient
{
    Task<FootballApiResponse<FootballApiLeaguesParameters, FootballApiLeague[]>> GetLeaguesAsync(
        FootballApiLeaguesParameters? parameters = null
    );
}

public class FootballApiClient(IHttpClientFactory factory, IOptions<FootballApiClientSettings> options)
    : IFootballApiClient
{
    private HttpClient Client
    {
        get
        {
            var client = factory.CreateClient(FootballApiClientSettings.ClientName);
            client.BaseAddress = new Uri(Settings.BaseUrl);
            client.DefaultRequestHeaders.Add("x-apisports-key", Settings.ApiKey);
            return client;
        }
    }

    private FootballApiClientSettings Settings => options?.Value ?? new FootballApiClientSettings();

    public async Task<FootballApiResponse<FootballApiLeaguesParameters, FootballApiLeague[]>> GetLeaguesAsync(
        FootballApiLeaguesParameters? parameters = null)
    {
        var response = await Client.GetAsync("/leagues");
        var json = await response.Content.ReadAsStringAsync();
        
        var result = await response.Content
            .ReadFromJsonAsync<FootballApiResponse<FootballApiLeaguesParameters, FootballApiLeague[]>>();
        return result!;
    }
}