namespace TaskFlow.Application.Projects;

public record ProjectDto(int Id, string Name, string? Description, DateTime CreatedAt);

public record ProjectWithCountDto(int Id, string Name, string? Description, DateTime CreatedAt, int EmployeeCount);

public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);
