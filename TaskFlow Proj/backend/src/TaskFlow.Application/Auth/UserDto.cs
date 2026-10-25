namespace TaskFlow.Application.Auth;

public record UserDto(
    int Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    string? EmployeeCode,
    IReadOnlyList<string> Permissions,
    // True while MFA is required but the user has not set it up yet; the client sends them to the setup screen.
    bool MfaSetupRequired = false);
