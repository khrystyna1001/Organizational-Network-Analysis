using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ONA.Graph.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior: logs the duration of every command/query. Logs the request TYPE only,
/// never its content (commands can carry personal data). Exceptions are rethrown unchanged;
/// the HTTP layer turns them into Problem Details.
/// </summary>
internal sealed class RequestLoggingBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next();
            logger.LogDebug("{Request} handled in {Elapsed:0.0} ms",
                typeof(TRequest).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogInformation("{Request} failed after {Elapsed:0.0} ms with {Error}",
                typeof(TRequest).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception.GetType().Name);
            throw;
        }
    }
}
