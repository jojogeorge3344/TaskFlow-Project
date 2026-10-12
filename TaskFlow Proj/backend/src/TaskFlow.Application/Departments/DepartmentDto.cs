namespace TaskFlow.Application.Departments;

public record DepartmentDto(int Id, string Name, string? Description, DateTime CreatedAt);

public record DepartmentWithCountDto(int Id, string Name, string? Description, DateTime CreatedAt, int EmployeeCount);

public record CreateDepartmentRequest(string Name, string? Description);

public record UpdateDepartmentRequest(string Name, string? Description);
