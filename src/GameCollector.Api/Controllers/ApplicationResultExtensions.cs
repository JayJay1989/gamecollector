using GameCollector.Api.Configuration;
using GameCollector.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameCollector.Api.Controllers;

internal static class ApplicationResultExtensions
{
    public static ObjectResult ToProblemResult(this ControllerBase controller, ApplicationError error)
    {
        var statusCode = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Title
        };
        ProblemDetailsExtensions.Enrich(controller.HttpContext, problemDetails, error.Code);

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
