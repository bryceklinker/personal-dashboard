using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public interface IFootballApiClient
{
    Task<FootballApiResponse<FootballApiLeaguesParameters, FootballApiLeague[]>> GetLeaguesAsync(
        FootballApiLeaguesParameters? parameters = null
    );
}

public class FootballApiClient(
    IHttpClientFactory factory,
    ILogger<FootballApiClient> logger,
    IOptions<FootballApiClientSettings> options)
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
        return await GetAsync<FootballApiLeaguesParameters, FootballApiLeague[]>("/leagues", parameters);
    }

    private async Task<FootballApiResponse<TParameters, TResponse>> GetAsync<TParameters, TResponse>(string path, TParameters? parameters) 
        where TParameters : FootballApiParameters
    {
        var pathAndQuery = parameters is null
            ? path
            : $"{path}?{parameters.AsQueryString()}";
        
        var response = await Client.GetAsync(pathAndQuery);
        var apiResponse = await response.Content.ReadFromJsonAsync<FootballApiResponse<TParameters, TResponse>>();
        return FootballApiException<TParameters, TResponse>.ThrowWithLogIfFailed(apiResponse, logger);
    }
}