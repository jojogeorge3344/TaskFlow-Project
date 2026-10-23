using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Api;

/// <summary>A save that lost a race (another request changed the same row first, for example an MFA check finishing while an
/// administrator edits the user) is a conflict the client can retry, not a server fault.</summary>
public class ConcurrencyExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The record was changed at the same time.",
                Detail = "Someone else changed this record while you were saving. Reload it and try again."
            },
            cancellationToken);
        return true;
    }
}
