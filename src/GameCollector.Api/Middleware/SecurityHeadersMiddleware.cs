namespace GameCollector.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.Append("Referrer-Policy", "no-referrer");
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next(context);
    }
}
