using System.Globalization;
using System.Threading.RateLimiting;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameCollector.Api.Configuration;

public static class ApiHardeningExtensions
{
    public static IServiceCollection AddApiHardening(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ApiHardeningOptions.SectionName).Get<ApiHardeningOptions>()
            ?? new ApiHardeningOptions();
        if (settings.MaximumRequestBodyBytes is < 1024 or > 16_777_216 ||
            settings.RateLimitPermitCount is < 1 or > 10_000 ||
            settings.RateLimitWindowSeconds is < 1 or > 3600 ||
            settings.RequestTimeoutSeconds is < 1 or > 300)
            throw new InvalidOperationException("ApiHardening configuration is outside its supported range.");

        services.Configure<ApiHardeningOptions>(configuration.GetSection(ApiHardeningOptions.SectionName));
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            options.Limits.MaxRequestBodySize = settings.MaximumRequestBodyBytes);
        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = TimeoutPolicy(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
            options.AddPolicy("ShortIntegrationTest", TimeoutPolicy(TimeSpan.FromMilliseconds(50)));
        });
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.RateLimitPermitCount,
                        Window = TimeSpan.FromSeconds(settings.RateLimitWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rejection, cancellationToken) =>
            {
                if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    rejection.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                var problem = CreateProblem(rejection.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Too many requests. Try again later.", ApiErrorCodes.RateLimitExceeded);
                rejection.HttpContext.Response.ContentType = "application/problem+json";
                await rejection.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };
        });
        return services;
    }

    private static ProblemDetails CreateProblem(HttpContext context, int status, string title, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        ProblemDetailsExtensions.Enrich(context, problem, code);
        return problem;
    }

    private static RequestTimeoutPolicy TimeoutPolicy(TimeSpan timeout) => new()
    {
        Timeout = timeout,
        TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
        WriteTimeoutResponse = async context =>
        {
            var problem = CreateProblem(context, StatusCodes.Status503ServiceUnavailable,
                "The request exceeded the server time limit.", ApiErrorCodes.RequestTimedOut);
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    };
}
