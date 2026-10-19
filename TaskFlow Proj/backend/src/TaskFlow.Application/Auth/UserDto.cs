namespace TaskFlow.Application.Auth;

public record UserDto(
    int Id,
    string FullName,
    string Username,
    string Email,
    string Role,
    string? EmployeeCode,
    IReadOnlyList<string> Permissions);
