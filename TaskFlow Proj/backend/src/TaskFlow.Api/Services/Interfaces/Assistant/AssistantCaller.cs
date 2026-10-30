using System.Security.Claims;

namespace TaskFlow.Api.Services.Interfaces.Assistant;

/// <summary>The signed-in user TFA acts as. Id and role are read from the validated token's claims: they are never
/// taken from the chat text or from anything the model returns.</summary>
public sealed class AssistantCaller
{
    public AssistantCaller(ClaimsPrincipal principal)
    {
        Principal = principal;
        UserId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("The signed-in user has no id claim.");
        Role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    public ClaimsPrincipal Principal { get; }
    public int UserId { get; }
    public string Role { get; }
}

/// <summary>The authorization policies TFA checks. The values are the names of the API's own policies, so a policy
/// change affects the REST API and TFA together.</summary>
public static class AssistantPolicies
{
    /// <summary>The policy that requires the <c>roles.manage</c> permission.</summary>
    public const string RolesManage = TaskFlow.Api.Common.Security.PermissionPolicy.Prefix + TaskFlow.Api.Common.Security.PermissionKeys.RolesManage;
}

/// <summary>Asks the application's authorization system whether the user may do something. TFA must go through this
/// for every action; it never decides on its own.</summary>
public interface IAssistantAuthorizer
{
    /// <summary>True when the policy succeeds for the user. An empty policy means "any signed-in user".</summary>
    Task<bool> IsAllowedAsync(ClaimsPrincipal user, string? policyName, CancellationToken cancellationToken = default);
}
