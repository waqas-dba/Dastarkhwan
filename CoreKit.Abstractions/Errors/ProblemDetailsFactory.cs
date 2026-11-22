using Microsoft.AspNetCore.Http;

namespace CoreKit.Abstractions.Errors;

/// <summary>
/// Standardized RFC 9457 Problem Details factory for consistent error responses across modules.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>Creates a Problem Details response for an error.</summary>
    /// <param name="errorCode">Stable error code (e.g., "validation_failed", "not_found").</param>
    /// <param name="title">Human-readable title for the error type.</param>
    /// <param name="detail">Specific details about this occurrence.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="instance">Request path (optional).</param>
    /// <param name="traceId">Trace identifier for correlation (optional).</param>
    public static IResult Create(
        string errorCode,
        string title,
        string detail,
        int statusCode,
        string? instance = null,
        string? traceId = null)
    {
        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://dastarkhwan.platform/errors/{errorCode}",
            ["title"] = title,
            ["status"] = statusCode,
            ["detail"] = detail,
            ["errorCode"] = errorCode
        };

        if (!string.IsNullOrEmpty(instance))
            problem["instance"] = instance;

        if (!string.IsNullOrEmpty(traceId))
            problem["traceId"] = traceId;

        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }

    /// <summary>Maps an error kind to standard HTTP status code and title.</summary>
    public static (int StatusCode, string Title) Map(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
        ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
        ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
        ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error")
    };
}

/// <summary>Standard error kinds matching RFC 9457 and common HTTP semantics.</summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Internal
}