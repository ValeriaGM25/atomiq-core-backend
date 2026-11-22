using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Atomiq.Domain;

namespace Atomiq.Infrastructure;

public sealed partial class AtomiqExceptionHandler(ILogger<AtomiqExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Path);

        var statusCode = exception is DomainException or Atomiq.Application.ApplicationException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        var title = statusCode == StatusCodes.Status400BadRequest
            ? "The request could not be processed."
            : "An unexpected error occurred.";

        httpContext.Response.StatusCode = statusCode;
        await Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            }).ExecuteAsync(httpContext);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string path);
}
