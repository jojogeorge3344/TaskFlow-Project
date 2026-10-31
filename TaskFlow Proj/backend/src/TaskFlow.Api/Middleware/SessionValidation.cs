using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Services.Auth;

namespace TaskFlow.Api.Middleware;

/// <summary>
/// Rejects an otherwise valid access token when its user is gone, inactive, or has changed
/// their username/password since the token was issued (security stamp mismatch). For a valid session it then
/// replaces the token's role claim and adds one permission claim per permission the user's role holds right now,
/// so a role or permission change applies to the next request without signing in again.
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

        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
        var session = await sessions.GetAsync(userId, context.HttpContext.RequestAborted);

        if (session is null || !session.IsActive || session.SecurityStamp != tokenStamp)
        {
            context.Fail("The session is no longer valid. Sign in again.");
            return;
        }

        if (principal.Identity is ClaimsIdentity identity)
        {
            foreach (var stale in identity.FindAll(identity.RoleClaimType)
                         .Concat(identity.FindAll(AuthClaims.Permission))
                         .Concat(identity.FindAll(AuthClaims.MfaSetupRequired))
                         .ToList())
            {
                identity.RemoveClaim(stale);
            }

            identity.AddClaim(new Claim(identity.RoleClaimType, session.RoleName));

            if (session.MfaSetupRequired)
            {
                identity.AddClaim(new Claim(AuthClaims.MfaSetupRequired, "true"));
            }

            identity.AddClaims(session.Permissions.Select(key => new Claim(AuthClaims.Permission, key)));
        }
    }
}
