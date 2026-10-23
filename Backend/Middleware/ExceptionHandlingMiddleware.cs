using System.Text.Json;
using Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Middleware;

/// <summary>
/// Centralized exception handling: converts known application exceptions into
/// consistent RFC 7807 ProblemDetails responses and hides unexpected server
/// errors (no stack traces, no exception details for HTTP 500).
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail) = exception switch
        {
            OrderValidationException ex => (StatusCodes.Status400BadRequest, "Order validation failed.", ex.Message),
            ArgumentException ex => (StatusCodes.Status400BadRequest, "Invalid request.", ex.Message),
            UnauthorizedException ex => (StatusCodes.Status401Unauthorized, "Authentication failed.", ex.Message),
            ForbiddenException ex => (StatusCodes.Status403Forbidden, "Access denied.", ex.Message),
            NotFoundException ex => (StatusCodes.Status404NotFound, "Resource not found.", ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, "Conflict.", ex.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict.",
                "The record was modified by another request. Please review the current state and retry."),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error.",
                "An unexpected error occurred. Please try again later.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled exception ({Status}) for {Method} {Path}: {Message}",
                status, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Detail = detail,
            Status = status,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }
}
