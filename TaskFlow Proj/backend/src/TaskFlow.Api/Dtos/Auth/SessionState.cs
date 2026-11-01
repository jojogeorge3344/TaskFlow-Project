namespace TaskFlow.Api.Dtos.Auth;

/// <summary>A user's session facts as they are now: what a token must still match, and the role and effective
/// permissions to put on the request.</summary>
public record SessionState(
    string SecurityStamp,
    bool IsActive,
    bool MfaSetupRequired,
    string RoleName,
    IReadOnlyList<string> Permissions);
