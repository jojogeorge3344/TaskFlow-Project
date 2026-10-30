namespace TaskFlow.Application.Projects;

public record ProjectDto(int Id, string Name, string? Description, DateTime CreatedAt);

public record ProjectWithCountDto(int Id, string Name, string? Description, DateTime CreatedAt, int EmployeeCount, IReadOnlyList<string> Managers);

public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);

/// <summary>One task mapped to a project, with the assignee's position on that project (null when they are not on its team).</summary>
public record ProjectTaskDto(
    int Id,
    string TaskCode,
    string Title,
    TaskFlow.Api.Models.Enums.TaskStatus Status,
    TaskFlow.Api.Models.Enums.TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    int AssignedToUserId,
    string AssignedToName,
    int? AssigneePositionId,
    string? AssigneePositionName);

/// <summary>Optional filters for a project's tasks; all given filters apply together.</summary>
public record ProjectTaskFilter(
    TaskFlow.Api.Models.Enums.TaskStatus? Status = null,
    TaskFlow.Api.Models.Enums.TaskPriority? Priority = null,
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    int? AssignedToUserId = null,
    int? PositionId = null);
