using TaskFlow.Infrastructure.Authentication;

namespace TaskFlow.Api.Authentication;

/// <summary>Until a signed-in user has set up an authenticator app, the API answers every request with 403 and the code
/// <c>mfa_setup_required</c>, except the few endpoints the setup screen needs. This is the real gate: the Angular screen
/// only mirrors it, so a user cannot get around it by calling the API directly.</summary>
public class MfaSetupGateMiddleware
{
    public const string ErrorCode = "mfa_setup_required";

    private static readonly string[] AllowedPrefixes =
    [
        "/api/v1/auth/",
        "/api/v1/users/me/mfa/"
    ];

    private static readonly string[] AllowedExact =
    [
        "/api/v1/users/me"
    ];

    private readonly RequestDelegate _next;

    public MfaSetupGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(claim => claim.Type == AuthClaims.MfaSetupRequired)
            && IsApiPath(context.Request.Path)
            && !IsAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = ErrorCode,
                error = "Set up two-step verification to continue."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowed(PathString path)
    {
        var value = path.Value ?? string.Empty;
        var trimmed = value.Length > 1 ? value.TrimEnd('/') : value;
        return AllowedExact.Any(exact => string.Equals(trimmed, exact, StringComparison.OrdinalIgnoreCase))
            || AllowedPrefixes.Any(prefix => (trimmed + "/").StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
