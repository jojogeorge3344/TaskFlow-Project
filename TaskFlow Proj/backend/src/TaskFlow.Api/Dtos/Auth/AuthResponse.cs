namespace TaskFlow.Api.Dtos.Auth;

public record AuthenticatedUserResponse(
    int Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    string? EmployeeCode,
    IReadOnlyList<string> Permissions,
    bool MfaSetupRequired = false);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    AuthenticatedUserResponse User);
