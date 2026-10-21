using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Infrastructure.Authentication;

namespace TaskFlow.Api.Authentication;

/// <summary>
/// After authentication, refuses every authorized request of a user who must still replace a temporary password
/// with a 403 "Password change required" problem (design D7). It is a 403 rather than a token failure on purpose:
/// a 401 would sign the user out and they could never reach the change-password step. Only the change-password,
/// logout and current-user routes stay open.
/// </summary>
public class PasswordChangeRequiredMiddleware
{
    public const string ProblemTitle = "Password change required";

    private static readonly (string Method, string Path)[] ExemptRoutes =
    [
        ("POST", "/api/v1/users/me/password"),
        ("POST", "/api/v1/auth/logout"),
        ("GET", "/api/v1/users/me")
    ];

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.FindFirstValue(AuthClaims.MustChangePassword) == "true"
            && RequiresAuthorization(context)
            && !IsExempt(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = ProblemTitle,
                    Detail = "You signed in with a temporary password. Change your password to continue."
                },
                options: null,
                contentType: "application/problem+json");
            return;
        }

        await _next(context);
    }

    private static bool RequiresAuthorization(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        return metadata is not null
            && metadata.GetMetadata<IAllowAnonymous>() is null
            && metadata.GetMetadata<IAuthorizeData>() is not null;
    }

    private static bool IsExempt(HttpRequest request) =>
        ExemptRoutes.Any(route =>
            string.Equals(route.Method, request.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(route.Path, request.Path.Value?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
}
