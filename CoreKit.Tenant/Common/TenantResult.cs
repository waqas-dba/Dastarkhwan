namespace CoreKit.Tenant.Common;

public enum TenantErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record TenantError(TenantErrorKind Kind, string Code, string Message);

/// <summary>
/// The outcome of a tenant service call. Expected problems come back as a value, not as an exception.
/// </summary>
public class TenantResult
{
    protected TenantResult(TenantError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public TenantError? Error { get; }

    public static TenantResult Success() => new(null);

    public static TenantResult Failure(TenantError error) => new(error);

    public static TenantResult<T> Success<T>(T value) => new(value, null);

    public static TenantResult<T> Failure<T>(TenantError error) => new(default, error);

    public static implicit operator TenantResult(TenantError error) => Failure(error);
}

public sealed class TenantResult<T> : TenantResult
{
    private readonly T? _value;

    internal TenantResult(T? value, TenantError? error) : base(error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");

    public static implicit operator TenantResult<T>(TenantError error) => Failure<T>(error);
}