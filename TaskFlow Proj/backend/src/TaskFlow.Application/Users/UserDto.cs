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
    string? DepartmentName,
    // Manager assignments are recorded data only; they do not change what anyone can see.
    int? ManagerId = null,
    string? ManagerName = null,
    // For a manager: the departments they are assigned to and the people who report to them.
    IReadOnlyList<ManagerDepartmentDto>? ManagerDepartments = null,
    IReadOnlyList<ManagerReportDto>? Reports = null);

public record ManagerDepartmentDto(int Id, string Name);

public record ManagerReportDto(int Id, string FullName);

/// <summary>Replaces a user's manager and, for a manager, the departments they are assigned to. Both fields are
/// explicit: a null manager clears it and an empty list removes every department.</summary>
public record ManagerAssignmentRequest(int? ManagerId, IReadOnlyCollection<int>? ManagerDepartmentIds);

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
