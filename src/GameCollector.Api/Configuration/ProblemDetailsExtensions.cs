using GameCollector.Contracts.Api;
using GameCollector.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GameCollector.Api.Configuration;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                Enrich(context.HttpContext, context.ProblemDetails);
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = actionContext =>
            {
                var problemDetailsFactory = actionContext.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                    actionContext.HttpContext,
                    actionContext.ModelState,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "One or more validation errors occurred.");

                Enrich(actionContext.HttpContext, problemDetails);

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }

    public static void Enrich(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        string? errorCode = null)
    {
        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        problemDetails.Type ??= $"https://httpstatuses.com/{statusCode}";
        problemDetails.Extensions.TryAdd("code", errorCode ?? GetErrorCode(statusCode));
        problemDetails.Extensions.TryAdd("traceId", httpContext.TraceIdentifier);

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
        {
            problemDetails.Extensions.TryAdd("correlationId", correlationId);
        }
    }

    private static string GetErrorCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ApiErrorCodes.InvalidRequest,
        StatusCodes.Status401Unauthorized => ApiErrorCodes.NotAuthenticated,
        StatusCodes.Status403Forbidden => ApiErrorCodes.NotAllowed,
        StatusCodes.Status404NotFound => ApiErrorCodes.EntityMissing,
        StatusCodes.Status409Conflict => ApiErrorCodes.Conflict,
        StatusCodes.Status422UnprocessableEntity => ApiErrorCodes.DomainValidationFailed,
        StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimitExceeded,
        StatusCodes.Status413PayloadTooLarge => ApiErrorCodes.RequestTooLarge,
        StatusCodes.Status503ServiceUnavailable => ApiErrorCodes.RequestTimedOut,
        _ => ApiErrorCodes.UnexpectedError
    };
}
