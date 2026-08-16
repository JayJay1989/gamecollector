using System.Diagnostics;

namespace GameCollector.Api.Middleware;

using GameCollector.Contracts.Users;

public sealed partial class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["CorrelationId"] = context.TraceIdentifier,
                    ["UserId"] = context.User.FindFirst("sub")?.Value,
                    ["DeviceId"] = context.Request.Headers[DeviceHeaders.DeviceId].FirstOrDefault()
                }))
                {
                    var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    LogRequestCompleted(
                        logger,
                        context.Request.Method,
                        context.Request.Path.Value,
                        context.Response.StatusCode,
                        elapsedMilliseconds);
                }
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "HTTP {RequestMethod} {RequestPath} returned {StatusCode} in {ElapsedMilliseconds:F1} ms")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string requestMethod,
        string? requestPath,
        int statusCode,
        double elapsedMilliseconds);
}
