namespace TaskFlow.Application.Departments;

public record DepartmentDto(int Id, string Name, string? Description, DateTime CreatedAt);

public record DepartmentWithCountsDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int ManagerCount,
    int EmployeeCount);

public record CreateDepartmentRequest(string Name, string? Description);

public record UpdateDepartmentRequest(string Name, string? Description);

/// <summary>A user shown in the department members dialog. IsActive is carried so inactive current members stay selectable for removal.</summary>
public record MemberRefDto(int Id, string FullName, string Username, string? Email, bool IsActive);

public record DepartmentMembersDto(int DepartmentId, IReadOnlyCollection<MemberRefDto> Managers, IReadOnlyCollection<MemberRefDto> Employees);

/// <summary>Full replacement of a department's membership in one request.</summary>
public record UpdateDepartmentMembersRequest(IReadOnlyCollection<int> ManagerIds, IReadOnlyCollection<int> EmployeeIds);
