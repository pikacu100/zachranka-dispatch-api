using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Errors;

public class NotFoundException(string message) : Exception(message);

/// <summary>The request is valid but breaks a business rule, e.g. assigning a unit that is already busy.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>
/// Turns domain exceptions into RFC 7807 ProblemDetails responses and does the exception logging.
/// The built-in ExceptionHandlerMiddleware log is switched off in appsettings.json, because it
/// would log every expected 404/409 as an "unhandled exception" error with a stack trace.
/// </summary>
public class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct
    )
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (0, ""),
        };

        if (status == 0)
        {
            // A real bug: log it and let the middleware return a generic 500 ProblemDetails.
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path
            );
            return false;
        }

        // Expected outcome of a request that broke a business rule, not an error in the app.
        logger.LogInformation(
            "{Method} {Path} -> {Status}: {Message}",
            context.Request.Method,
            context.Request.Path,
            status,
            exception.Message
        );

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = exception.Message,
                },
            }
        );
    }
}
