using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Authentication;

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

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var cancellationToken = context.HttpContext.RequestAborted;
        var user = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.SecurityStamp,
                candidate.IsActive,
                candidate.MfaEnabledAt,
                RoleName = candidate.Role.Name,
                PermissionKeys = candidate.Role.Permissions.Select(grant => grant.Permission.Key).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsActive || user.SecurityStamp != tokenStamp)
        {
            context.Fail("The session is no longer valid. Sign in again.");
            return;
        }

        if (principal.Identity is ClaimsIdentity identity)
        {
            foreach (var stale in identity.FindAll(identity.RoleClaimType).Concat(identity.FindAll(AuthClaims.Permission)).ToList())
            {
                identity.RemoveClaim(stale);
            }

            identity.AddClaim(new Claim(identity.RoleClaimType, user.RoleName));

            var mfa = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaskFlow.Application.Mfa.MfaOptions>>().Value;
            foreach (var stale in identity.FindAll(AuthClaims.MfaSetupRequired).ToList())
            {
                identity.RemoveClaim(stale);
            }

            if (mfa.Required && user.MfaEnabledAt is null)
            {
                identity.AddClaim(new Claim(AuthClaims.MfaSetupRequired, "true"));
            }

            identity.AddClaims(TaskFlow.Api.Common.Security.EffectivePermissions.Expand(user.PermissionKeys).Select(key => new Claim(AuthClaims.Permission, key)));
        }
    }
}
