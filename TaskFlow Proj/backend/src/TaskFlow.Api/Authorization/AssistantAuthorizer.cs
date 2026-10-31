using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TaskFlow.Api.Services.Assistant.Access;

namespace TaskFlow.Api.Authorization;

/// <summary>Answers TFA's "may this user do that?" with the application's own authorization policies, registered in
/// <c>Program.cs</c> and used by the REST controllers. TFA and the API therefore cannot disagree, and a policy change
/// applies to both at once.</summary>
public class AssistantAuthorizer : IAssistantAuthorizer
{
    private readonly IAuthorizationService _authorization;

    public AssistantAuthorizer(IAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public async Task<bool> IsAllowedAsync(ClaimsPrincipal user, string? policyName, CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (string.IsNullOrEmpty(policyName))
        {
            return true;
        }

        var result = await _authorization.AuthorizeAsync(user, policyName);
        return result.Succeeded;
    }
}
