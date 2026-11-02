using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Errors;

public class NotFoundException(string message) : Exception(message);

/// <summary>The request is valid but breaks a business rule, e.g. assigning a unit that is already busy.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Turns domain exceptions into RFC 7807 ProblemDetails responses.</summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
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
            return false;

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
