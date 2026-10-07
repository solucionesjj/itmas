using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.ErrorHandling;

/// <summary>
/// Centralised translation of exceptions into RFC 9457 Problem Details. Responses never contain stack traces,
/// exception messages of unexpected errors, infrastructure details or secrets; the <c>traceId</c> extension
/// correlates the response with the server logs.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away: nothing to answer and nothing to alert on.
            return true;
        }

        var problem = exception switch
        {
            // Domain messages are written to be client-safe (see DomainException).
            DomainException domainException => Create(StatusCodes.Status422UnprocessableEntity, "Regla de negocio no satisfecha.", domainException.Message),
            NotFoundException => Create(StatusCodes.Status404NotFound, "Recurso no encontrado.", null),
            // Application messages of these types are written to be client-safe.
            ForbiddenException forbidden => Create(StatusCodes.Status403Forbidden, "Operación no permitida.", forbidden.Message),
            ConflictException conflict => Create(StatusCodes.Status409Conflict, "Conflicto con el estado actual.", conflict.Message),
            PreconditionFailedException precondition => Create(StatusCodes.Status412PreconditionFailed, "El recurso cambió.", precondition.Message),
            BadHttpRequestException badRequest => Create(badRequest.StatusCode, "Solicitud inválida.", null),
            _ => Create(StatusCodes.Status500InternalServerError, "Se produjo un error inesperado.", null),
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogHandledException(logger, exception.GetType().Name, problem.Status, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Create(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ExceptionType} mapped to HTTP {StatusCode} for {Method} {Path}")]
    private static partial void LogHandledException(ILogger logger, string exceptionType, int? statusCode, string method, PathString path);
}
