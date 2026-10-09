using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Common;


public enum IamErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record IamError(IamErrorKind Kind, string Code, string Message);

/// <summary>
/// The outcome of a service call. Expected problems (wrong password, duplicate email)
/// come back as an error value, not as an exception.
/// </summary>
public class IamResult
{
    protected IamResult(IamError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public IamError? Error { get; }

    public static IamResult Success() => new(null);

    public static IamResult Failure(IamError error) => new(error);

    public static IamResult<T> Success<T>(T value) => new(value, null);

    public static IamResult<T> Failure<T>(IamError error) => new(default, error);

    public static implicit operator IamResult(IamError error) => Failure(error);
}

public sealed class IamResult<T> : IamResult
{
    private readonly T? _value;

    public IamResult(T? value, IamError? error) : base(error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");

    public static implicit operator IamResult<T>(IamError error) => Failure<T>(error);
}
