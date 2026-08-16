using GameCollector.Api.Configuration;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameCollector.Api.Middleware;

public sealed class RequestSizeLimitMiddleware(RequestDelegate next, IOptions<ApiHardeningOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > options.Value.MaximumRequestBodyBytes)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "The request body is too large."
            };
            ProblemDetailsExtensions.Enrich(context, problem, ApiErrorCodes.RequestTooLarge);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }
        await next(context);
    }
}
