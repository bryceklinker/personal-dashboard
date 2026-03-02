using System.Net.Http.Json;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Web.Host.Common.Apis;

public class PersonalDashboardApiClient(HttpClient client)
{
    public async Task<PagedListResultModel<FootballLeagueModel>> GetLeaguesAsync(PagedListParameters? parameters = null)
    {
        var queryParameters = parameters ?? PagedListParameters.Default();
        return await GetJsonAsync<PagedListResultModel<FootballLeagueModel>>
            (
                $"/leagues?{queryParameters.ToQueryString()}"
            )
            .ConfigureAwait(false);
    }

    private async Task<TResult> GetJsonAsync<TResult>(string route)
    {
        var response = await client.GetAsync(route).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TResult>().ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("request returned null json result");
    }
}