using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Users;

public record UserDto(
    int Id,
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    Role Role,
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
    Role Role,
    bool IsActive,
    int? DepartmentId);

public record UpdateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    Role Role,
    int? DepartmentId,
    // Optional new password; null or empty keeps the current one.
    string? Password = null);

public record UserStatusRequest(bool IsActive);

public record UserListFilter(
    string? Search,
    int? DepartmentId,
    Role? Role,
    bool? IsActive);
