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
    int? ProjectId,
    string? ProjectName,
    int? DepartmentId = null,
    string? DepartmentName = null,
    // Manager assignments are recorded data only; they do not change what anyone can see.
    int? ManagerId = null,
    string? ManagerName = null,
    // For a manager: the projects they oversee and the people who report to them.
    IReadOnlyList<ManagerProjectDto>? ManagerProjects = null,
    IReadOnlyList<ManagerReportDto>? Reports = null,
    // Authenticator MFA status. The key is never part of any DTO.
    bool MfaEnabled = false,
    int BackupCodesRemaining = 0,
    // Contact number in international format; null when none has been given.
    string? MobileNumber = null);

public record ManagerProjectDto(int Id, string Name);

public record ManagerReportDto(int Id, string FullName);

/// <summary>Replaces a user's manager and, for a manager, the projects they oversee. Both fields are
/// explicit: a null manager clears it and an empty list removes every project.</summary>
public record ManagerAssignmentRequest(int? ManagerId, IReadOnlyCollection<int>? ManagerProjectIds);

public record CreateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    string Password,
    int RoleId,
    bool IsActive,
    int? ProjectId,
    int? DepartmentId = null);

public record UpdateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    int RoleId,
    int? ProjectId,
    int? DepartmentId = null,
    // Optional new password; null or empty keeps the current one.
    string? Password = null);

public record UserStatusRequest(bool IsActive);

public record UserListFilter(
    string? Search,
    int? ProjectId,
    string? Role,
    bool? IsActive);
