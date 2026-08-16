namespace GameCollector.Contracts.Api;

public static class ApiErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string NotAuthenticated = "not_authenticated";
    public const string NotAllowed = "not_allowed";
    public const string EntityMissing = "entity_missing";
    public const string Conflict = "conflict";
    public const string DomainValidationFailed = "domain_validation_failed";
    public const string RateLimitExceeded = "rate_limit_exceeded";
    public const string RequestTooLarge = "request_too_large";
    public const string RequestTimedOut = "request_timed_out";
    public const string UnexpectedError = "unexpected_error";
}
