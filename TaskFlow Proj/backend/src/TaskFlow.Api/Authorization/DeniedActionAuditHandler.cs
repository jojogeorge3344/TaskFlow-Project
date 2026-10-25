using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace TaskFlow.Api.Authorization;

/// <summary>Records every refused authorization as a denied action in the audit trail: a caller who
/// holds some other permission attempts a protected action and the policy says no (Requirement 18,
/// "the change is refused and recorded as a denied action"). The entry carries identifiers only -
/// calling user id, HTTP method, request path and the policy name - never a credential, request
/// body or query string. The refusal itself is still executed by the default handler, so responses
/// keep their usual 401/403 shape.</summary>
public sealed class DeniedActionAuditHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    private readonly ILogger<DeniedActionAuditHandler> _logger;

    public DeniedActionAuditHandler(ILogger<DeniedActionAuditHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var policyName = context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>()?.Policy ?? "unspecified";
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

            _logger.LogWarning(
                "Denied action: user {UserId} attempted {Method} {Path}; authorization policy {Policy} refused.",
                userId,
                context.Request.Method,
                context.Request.Path,
                policyName);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
