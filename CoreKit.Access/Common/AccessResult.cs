namespace CoreKit.Access.Common;

public enum AccessErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record AccessError(AccessErrorKind Kind, string Code, string Message);

/// <summary>The outcome of an Access call. Expected problems come back as a value, not as an exception.</summary>
public class AccessResult
{
    protected AccessResult(AccessError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public AccessError? Error { get; }

    public static AccessResult Success() => new(null);

    public static AccessResult Failure(AccessError error) => new(error);

    public static AccessResult<T> Success<T>(T value) => new(value, null);

    public static AccessResult<T> Failure<T>(AccessError error) => new(default, error);

    public static implicit operator AccessResult(AccessError error) => Failure(error);
}

public sealed class AccessResult<T> : AccessResult
{
    private readonly T? _value;

    internal AccessResult(T? value, AccessError? error) : base(error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");

    public static implicit operator AccessResult<T>(AccessError error) => Failure<T>(error);
}