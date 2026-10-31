using System.Security.Claims;

namespace TaskFlow.Api.Services.Assistant;

/// <summary>Asks the application's authorization system whether the user may do something. TFA must go through this
/// for every action; it never decides on its own.</summary>
public interface IAssistantAuthorizer
{
    /// <summary>True when the policy succeeds for the user. An empty policy means "any signed-in user".</summary>
    Task<bool> IsAllowedAsync(ClaimsPrincipal user, string? policyName, CancellationToken cancellationToken = default);
}
