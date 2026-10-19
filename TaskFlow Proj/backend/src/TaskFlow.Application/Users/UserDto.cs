namespace TaskFlow.Application.Users;

public record UserDto(
    int Id,
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    int RoleId,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt,
    int? DepartmentId,
    string? DepartmentName);

public record CreateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    string Password,
    int RoleId,
    bool IsActive,
    int? DepartmentId);

public record UpdateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    int RoleId,
    int? DepartmentId,
    // Optional new password; null or empty keeps the current one.
    string? Password = null);

public record UserStatusRequest(bool IsActive);

public record UserListFilter(
    string? Search,
    int? DepartmentId,
    string? Role,
    bool? IsActive);
