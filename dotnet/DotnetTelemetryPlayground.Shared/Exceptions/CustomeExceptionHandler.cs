using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DotnetTelemetryPlayground.Shared.Exceptions;

/// <summary>
/// Custom exception handler to log exceptions
/// </summary>
/// <param name="logger">The logger instance</param>
public class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Log the exception details
        StringBuilder sb = new();
        Exception? innerException = exception.InnerException;
        int count = 0;
        string errorMessage;
        sb.AppendLine("An error occurred while processing your request.");
        sb.AppendLine($"Error Message: {exception.Message}");
        while (innerException != null) {
            sb.AppendLine($"Inner Exception[{++count}]: {innerException.Message}");
            innerException = innerException.InnerException;
        }
        errorMessage = sb.ToString();
        logger.LogError(errorMessage);

        // Prepare ProblemDetails result
        (string Detail, string Title, int StatusCode) details = exception switch
        {
            ValidationException =>
            (
                errorMessage,
                exception.GetType().Name,
                StatusCodes.Status400BadRequest
            ),
            NotFoundException =>
            (
                errorMessage,
                exception.GetType().Name,
                StatusCodes.Status404NotFound
            ),
            _ =>
            (
                "An error occurred while processing your request.",
                exception.GetType().Name,
                StatusCodes.Status500InternalServerError
            )
        };
        ProblemDetails problemDetails = new ()
        {
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions.Add("traceId", httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}
