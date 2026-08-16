using System.Diagnostics;

namespace GameCollector.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    private const int MaximumCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var suppliedId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(suppliedId) && suppliedId.Length <= MaximumCorrelationIdLength &&
            suppliedId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))
        {
            return suppliedId;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
