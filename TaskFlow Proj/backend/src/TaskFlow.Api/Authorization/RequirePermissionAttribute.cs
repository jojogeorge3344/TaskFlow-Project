using Microsoft.AspNetCore.Authorization;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Authorization;

/// <summary>Requires the signed-in user's role to hold the permission. The permission claims are refreshed from the
/// database on every request, so a change to a role applies to the user's very next call.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionKey)
        : base(PermissionPolicy.NameFor(permissionKey))
    {
        PermissionKey = permissionKey;
    }

    public string PermissionKey { get; }
}
