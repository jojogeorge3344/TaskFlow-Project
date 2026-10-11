namespace TaskFlow.Api.Contracts;

public record AuthenticatedUserResponse(
    int Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    string? EmployeeCode);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    AuthenticatedUserResponse User);
