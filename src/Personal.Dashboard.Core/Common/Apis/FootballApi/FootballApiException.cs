namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public class FootballApiException<TParameters, TResponse> : Exception
    where TParameters : class
{
    public FootballApiResponse<TParameters, TResponse> Response { get; }
    
    public FootballApiException(FootballApiResponse<TParameters, TResponse> response)
        : base($"Errors: {string.Join(", ", response.Errors)}")
    {
        Response = response; 
    }

    public static FootballApiResponse<TParameters, TResponse> ThrowIfFailed<TParameters, TResponse>(FootballApiResponse<TParameters, TResponse>? response) 
        where TParameters : class
    {
        return response.Errors.Length != 0 
            ? throw new FootballApiException<TParameters, TResponse>(response) 
            : response;
    }
}