namespace GameCollector.Application.Common;

public enum ApplicationErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed record ApplicationError(string Code, string Title, ApplicationErrorType Type);
