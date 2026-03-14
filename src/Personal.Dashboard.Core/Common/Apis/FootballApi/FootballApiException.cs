using Microsoft.Extensions.Logging;

namespace Personal.Dashboard.Core.Common.Apis.FootballApi;

public class FootballApiException<TResponse>(
    FootballApiResponse<TResponse> response
)
    : Exception($"Errors: {string.Join(", ", response.Errors)}")
{
    public FootballApiResponse<TResponse> Response { get; } = response;

    public static FootballApiResponse<TResult> ThrowIfFailed<TResult>(
        FootballApiResponse<TResult>? response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Errors.Count != 0
            ? throw new FootballApiException<TResult>(response)
            : response;
    }

    public static FootballApiResponse<TResult> ThrowWithLogIfFailed<TResult>(
        FootballApiResponse<TResult>? response, ILogger logger)
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