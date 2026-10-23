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
    // The projects this user is assigned to, each with a percentage and a position.
    IReadOnlyList<ProjectAllocationDto> ProjectAllocations,
    // The departments this user belongs to, and the managers of those departments (derived, never stored on the user).
    IReadOnlyList<UserDepartmentDto> Departments,
    IReadOnlyList<UserManagerDto> Managers,
    // For a manager: the projects they oversee and the people who report to them.
    IReadOnlyList<ManagerProjectDto>? ManagerProjects = null,
    IReadOnlyList<ManagerReportDto>? Reports = null,
    // Authenticator MFA status. The key is never part of any DTO.
    bool MfaEnabled = false,
    int BackupCodesRemaining = 0,
    // Contact number in international format; null when none has been given.
    string? MobileNumber = null)
{
    /// <summary>Sum of the percentages across all projects; never above 100.</summary>
    public int TotalAllocatedPercent => ProjectAllocations.Sum(allocation => allocation.Percent);

    public int RemainingPercent => 100 - TotalAllocatedPercent;
}

public record ProjectAllocationDto(int ProjectId, string ProjectName, int PositionId, string PositionName, int Percent);

public record UserDepartmentDto(int Id, string Name);

public record UserManagerDto(int Id, string FullName);

/// <summary>One project row from a form: the project, the position held there and the whole percentage (1 to 100).</summary>
public record ProjectAllocationInput(int ProjectId, int PositionId, int Percent);

public record ManagerProjectDto(int Id, string Name);

public record ManagerReportDto(int Id, string FullName);

public record CreateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    string Password,
    int RoleId,
    bool IsActive,
    IReadOnlyList<ProjectAllocationInput>? ProjectAllocations = null,
    IReadOnlyList<int>? DepartmentIds = null);

public record UpdateUserRequest(
    string? EmployeeCode,
    string FullName,
    string Username,
    string Email,
    int RoleId,
    IReadOnlyList<ProjectAllocationInput>? ProjectAllocations = null,
    IReadOnlyList<int>? DepartmentIds = null,
    // Optional new password; null or empty keeps the current one.
    string? Password = null);

public record UserStatusRequest(bool IsActive);

/// <summary>ProjectId and DepartmentId match anyone with that project or department among their memberships.</summary>
public record UserListFilter(
    string? Search,
    int? ProjectId,
    string? Role,
    bool? IsActive,
    int? DepartmentId = null);
