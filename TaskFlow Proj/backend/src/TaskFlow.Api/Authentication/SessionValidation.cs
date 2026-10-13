using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.Authentication;

/// <summary>
/// Rejects an otherwise valid access token when its user is gone, inactive, or has changed
/// their username/password since the token was issued (security stamp mismatch).
/// </summary>
public static class SessionValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail("Invalid token subject.");
            return;
        }

        // Tokens issued before stamps existed carry none; they match users whose stamp is still empty.
        var tokenStamp = principal!.FindFirstValue(AuthClaims.SecurityStamp) ?? string.Empty;

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.SecurityStamp, candidate.IsActive })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive || user.SecurityStamp != tokenStamp)
        {
            context.Fail("The session is no longer valid. Sign in again.");
        }
    }
}
