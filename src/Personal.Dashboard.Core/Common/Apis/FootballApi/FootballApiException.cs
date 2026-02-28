using Microsoft.Extensions.Logging;

namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public class FootballApiException<TParameters, TResponse>(
    FootballApiResponse<TParameters, TResponse> response
)
    : Exception($"Errors: {string.Join(", ", response.Errors)}")
    where TParameters : class
{
    public FootballApiResponse<TParameters, TResponse> Response { get; } = response;

    public static FootballApiResponse<TParams, TResult> ThrowIfFailed<TParams, TResult>(
        FootballApiResponse<TParams, TResult>? response)
        where TParams : FootballApiParameters
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Errors.Length != 0
            ? throw new FootballApiException<TParams, TResult>(response)
            : response;
    }

    public static FootballApiResponse<TParams, TResult> ThrowWithLogIfFailed<TParams, TResult>(
        FootballApiResponse<TParams, TResult>? response, ILogger logger)
        where TParams : FootballApiParameters
    {
        try
        {
            return ThrowIfFailed(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, exception.Message);
            throw;
        }
    }
}