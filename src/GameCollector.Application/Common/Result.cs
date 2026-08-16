namespace GameCollector.Application.Common;

public sealed class Result<T>
{
    private Result(T? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ApplicationError? Error { get; }

    internal static Result<T> CreateSuccess(T value) => new(value, null);

    internal static Result<T> CreateFailure(ApplicationError error) => new(default, error);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.CreateSuccess(value);

    public static Result<T> Failure<T>(ApplicationError error) => Result<T>.CreateFailure(error);
}
