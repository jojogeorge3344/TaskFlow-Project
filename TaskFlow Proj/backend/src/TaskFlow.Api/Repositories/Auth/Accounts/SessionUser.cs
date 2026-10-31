namespace TaskFlow.Api.Repositories.Auth.Accounts;

/// <summary>What validating a session needs to know about its user right now.</summary>
public record SessionUser(
    string SecurityStamp,
    bool IsActive,
    DateTime? MfaEnabledAt,
    string RoleName,
    IReadOnlyList<string> PermissionKeys);
