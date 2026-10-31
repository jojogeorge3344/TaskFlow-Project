using System.Diagnostics;
using System.Security.Claims;

namespace TaskFlow.Api.Middleware;

/// <summary>Writes one log entry per request: method, path, status, elapsed time, the signed-in user's id and the trace id.
/// Bodies are never logged, because they carry passwords, authenticator codes and mail content.</summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            // Read after the pipeline ran: authentication has set the user by then, and the status is final.
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms for user {UserId} (trace {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                GlobalExceptionHandler.TraceIdFor(context));
        }
    }
}
