using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Common;


public enum IdentityErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record IdentityError(IdentityErrorKind Kind, string Code, string Message);

/// <summary>
/// The outcome of a service call. Expected problems (wrong password, duplicate email)
/// come back as an error value, not as an exception.
/// </summary>
public class IdentityResult
{
    protected IdentityResult(IdentityError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public IdentityError? Error { get; }

    public static IdentityResult Success() => new(null);

    public static IdentityResult Failure(IdentityError error) => new(error);

    public static IdentityResult<T> Success<T>(T value) => new(value, null);

    public static IdentityResult<T> Failure<T>(IdentityError error) => new(default, error);

    public static implicit operator IdentityResult(IdentityError error) => Failure(error);
}

public sealed class IdentityResult<T> : IdentityResult
{
    private readonly T? _value;

    public IdentityResult(T? value, IdentityError? error) : base(error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");

    public static implicit operator IdentityResult<T>(IdentityError error) => Failure<T>(error);
}
