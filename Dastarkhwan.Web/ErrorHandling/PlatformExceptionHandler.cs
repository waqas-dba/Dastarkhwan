using Dastarkhwan.Web.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
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

        // Log the full exception internally for diagnosis, but never emit raw
        // exception messages to the client.
        if (logException is not null)
        {
            _logger.Log(
                logLevel,
                logException,
                "Unhandled exception {ErrorCode} at {Path} (TraceId: {TraceId})",
                errorCode, context.Request.Path, traceId);
        }
        else if (logLevel == LogLevel.Error || logLevel == LogLevel.Critical)
        {
            _logger.Log(
                logLevel,
                "Unhandled exception {ErrorCode} at {Path} (TraceId: {TraceId})",
                errorCode, context.Request.Path, traceId);
        }
        else if (logLevel == LogLevel.Warning)
        {
            _logger.LogWarning(
                "Handled exception {ErrorCode} at {Path} (TraceId: {TraceId})",
                errorCode, context.Request.Path, traceId);
        }

        // If the response has already started, the status code and headers cannot be
        // changed. Abort safely rather than throwing a second exception.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Exception {ErrorCode} at {Path} (TraceId: {TraceId}) could not be written: response already started.",
                errorCode, context.Request.Path, traceId);
            return;
        }

        var result = ProblemDetailsFactory.Create(
            errorCode: errorCode,
            title: title,
            detail: detail,
            statusCode: statusCode,
            instance: context.Request.Path.Value,
            traceId: traceId);

        await result.ExecuteAsync(context);
    }

    private static (int StatusCode, string Title, string ErrorCode, string Detail, LogLevel LogLevel, Exception? LogException) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentException argEx =>
                (StatusCodes.Status400BadRequest, "Invalid request", ErrorCodes.InvalidRequest,
                    "The request is invalid.", LogLevel.Warning, argEx),

            InvalidOperationException invOpEx when IsConflict(invOpEx) =>
                (StatusCodes.Status409Conflict, "Conflict", ErrorCodes.Conflict,
                    "The request conflicts with the current state.", LogLevel.Warning, invOpEx),

            InvalidOperationException invOpEx =>
                (StatusCodes.Status400BadRequest, "Invalid request", ErrorCodes.InvalidRequest,
                    "The request cannot be processed.", LogLevel.Warning, invOpEx),

            KeyNotFoundException knfEx =>
                (StatusCodes.Status404NotFound, "Not found", ErrorCodes.NotFound,
                    "The requested resource was not found.", LogLevel.Warning, knfEx),

            UnauthorizedAccessException uaEx =>
                (StatusCodes.Status401Unauthorized, "Unauthorized", ErrorCodes.Unauthorized,
                    "Authentication is required.", LogLevel.Warning, uaEx),

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

    /// <summary>
    /// Detects constraint violations using provider SQLSTATE codes where available,
    /// falling back to message matching only when the provider does not expose codes.
    /// </summary>
    private static bool IsConstraintViolation(DbUpdateException dbEx)
    {
        // Npgsql exposes SQLSTATE codes directly: 23505 unique_violation,
        // 23514 check_violation, 23503 foreign_key_violation, 23P01 exclusion_violation.
        var postgres = dbEx.InnerException as PostgresException;
        if (postgres is not null)
        {
            return postgres.SqlState switch
            {
                "23505" => true, // unique_violation
                "23514" => true, // check_violation
                "23503" => true, // foreign_key_violation
                "23P01" => true, // exclusion_violation
                _ => false
            };
        }

        // Fallback for providers that do not expose SQLSTATE codes.
        var message = (dbEx.InnerException?.Message ?? dbEx.Message)?.ToLowerInvariant() ?? string.Empty;
        return message.Contains("unique") ||
               message.Contains("duplicate") ||
               message.Contains("check constraint") ||
               message.Contains("violat");
    }

    private static bool IsConflict(InvalidOperationException ex)
    {
        var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
        return message.Contains("conflict");
    }
}

/// <summary>Extension to register the platform exception middleware.</summary>
public static class PlatformExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UsePlatformExceptionHandler(this IApplicationBuilder builder)
        => builder.UseMiddleware<PlatformExceptionMiddleware>();
}
