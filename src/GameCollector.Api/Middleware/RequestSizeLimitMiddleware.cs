using GameCollector.Api.Configuration;
using GameCollector.Contracts.Api;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameCollector.Api.Middleware;

public sealed class RequestSizeLimitMiddleware(RequestDelegate next, IOptions<ApiHardeningOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpointLimit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
        var maximumBytes = endpointLimit ?? options.Value.MaximumRequestBodyBytes;
        if (context.Request.ContentLength > maximumBytes)
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
