namespace Sentinel.SharedKernel;

public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message) => new("validation", message);

    public static Error Conflict(string message) => new("conflict", message);

    public static Error NotFound(string message) => new("not_found", message);

    public static Error Unauthorized(string message) => new("unauthorized", message);

    public static Error Forbidden(string message) => new("forbidden", message);
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("Successful results cannot carry an error.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("Failed results must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, null);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");
}
