using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Api.Middleware;

/// <summary>The one place that turns an exception into an HTTP response. An <see cref="AppException"/> becomes a problem
/// response with its own status, title and message. A lost save race becomes a 409 the client can retry. Anything else is a
/// fault: it is logged in full and the client only gets a generic 500, so internal messages never leave the server.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public const string GenericErrorDetail = "Something went wrong. Please try again.";

    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ProblemDetailsFactory problemDetailsFactory, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsFactory = problemDetailsFactory;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestValidationException validation => ValidationProblem(httpContext, validation.Errors),
            AppException known => Problem(httpContext, known.StatusCode, known.Title, known.Message),

            // Another request changed the same row first, for example an MFA check finishing while an administrator edits
            // the user. That is a conflict the client can retry, not a server fault.
            DbUpdateConcurrencyException => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                "The record was changed at the same time.",
                "Someone else changed this record while you were saving. Reload it and try again."),

            _ => null
        };

        if (problem is null)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} (trace {TraceId})",
                httpContext.Request.Method,
                httpContext.Request.Path,
                TraceIdFor(httpContext));
            problem = Problem(httpContext, StatusCodes.Status500InternalServerError, null, GenericErrorDetail);
        }

        if (exception is AppException { RetryAfter: { } retryAfter })
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, "application/problem+json", cancellationToken);
        return true;
    }

    /// <summary>The trace id the problem response carries, so a client report can be matched to the log entry.</summary>
    public static string TraceIdFor(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;

    private ProblemDetails Problem(HttpContext httpContext, int status, string? title, string detail) =>
        _problemDetailsFactory.CreateProblemDetails(httpContext, status, title, detail: detail);

    private ValidationProblemDetails ValidationProblem(HttpContext httpContext, IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        return _problemDetailsFactory.CreateValidationProblemDetails(httpContext, modelState, StatusCodes.Status400BadRequest);
    }
}
