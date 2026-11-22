using Dastarkhwan.Web.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Dastarkhwan.Web.ErrorHandling;

/// <summary>Central exception-handling middleware converting platform exceptions to RFC 9457 Problem Details.</summary>
public sealed class PlatformExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PlatformExceptionMiddleware> _logger;

    public PlatformExceptionMiddleware(RequestDelegate next, ILogger<PlatformExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (statusCode, title, errorCode, detail, logLevel, logException) = MapException(exception);

        if (logLevel == LogLevel.Error || logLevel == LogLevel.Critical)
        {
            _logger.Log(
                logLevel,
                logException,
                "Unhandled exception {ErrorCode} at {Path} (TraceId: {TraceId}): {Message}",
                errorCode, context.Request.Path, traceId, exception.Message);
        }
        else if (logLevel == LogLevel.Warning)
        {
            _logger.LogWarning(
                "Handled exception {ErrorCode} at {Path} (TraceId: {TraceId}): {Message}",
                errorCode, context.Request.Path, traceId, exception.Message);
        }

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://dastarkhwan.platform/errors/{errorCode}",
            ["title"] = title,
            ["status"] = statusCode,
            ["detail"] = detail,
            ["instance"] = context.Request.Path,
            ["errorCode"] = errorCode,
            ["traceId"] = traceId
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int StatusCode, string Title, string ErrorCode, string Detail, LogLevel LogLevel, Exception? LogException) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentException argEx =>
                (StatusCodes.Status400BadRequest, "Invalid request", ErrorCodes.InvalidRequest,
                    argEx.Message, LogLevel.Warning, null),

            InvalidOperationException invOpEx when invOpEx.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status409Conflict, "Conflict", ErrorCodes.Conflict,
                    invOpEx.Message, LogLevel.Warning, null),

            InvalidOperationException invOpEx =>
                (StatusCodes.Status400BadRequest, "Invalid request", ErrorCodes.InvalidRequest,
                    invOpEx.Message, LogLevel.Warning, null),

            KeyNotFoundException knfEx =>
                (StatusCodes.Status404NotFound, "Not found", ErrorCodes.NotFound,
                    knfEx.Message, LogLevel.Warning, null),

            UnauthorizedAccessException uaEx =>
                (StatusCodes.Status401Unauthorized, "Unauthorized", ErrorCodes.Unauthorized,
                    uaEx.Message, LogLevel.Warning, null),

            DbUpdateConcurrencyException concEx =>
                (StatusCodes.Status409Conflict, "Concurrency conflict", ErrorCodes.ConcurrencyFailure,
                    "The record was modified by another request. Please reload and try again.", LogLevel.Warning, concEx),

            DbUpdateException dbEx when IsConstraintViolation(dbEx) =>
                (StatusCodes.Status409Conflict, "Conflict", ErrorCodes.Conflict,
                    "The request conflicts with an existing record or constraint.", LogLevel.Warning, dbEx),

            DbUpdateException dbEx =>
                (StatusCodes.Status500InternalServerError, "Database error", ErrorCodes.DatabaseUnavailable,
                    "A database operation failed.", LogLevel.Error, dbEx),

            OperationCanceledException oce =>
                (StatusCodes.Status408RequestTimeout, "Request cancelled", ErrorCodes.RequestCancelled,
                    "The request was cancelled before completion.", LogLevel.Warning, oce),

            _ =>
                (StatusCodes.Status500InternalServerError, "Internal server error", ErrorCodes.InternalError,
                    "An unexpected error occurred.", LogLevel.Critical, exception)
        };
    }

    private static bool IsConstraintViolation(DbUpdateException dbEx)
    {
        var message = (dbEx.InnerException?.Message ?? dbEx.Message)?.ToLowerInvariant() ?? string.Empty;
        return message.Contains("unique") ||
               message.Contains("duplicate") ||
               message.Contains("check constraint") ||
               message.Contains("violat");
    }
}

/// <summary>Extension to register the platform exception middleware.</summary>
public static class PlatformExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UsePlatformExceptionHandler(this IApplicationBuilder builder)
        => builder.UseMiddleware<PlatformExceptionMiddleware>();
}
