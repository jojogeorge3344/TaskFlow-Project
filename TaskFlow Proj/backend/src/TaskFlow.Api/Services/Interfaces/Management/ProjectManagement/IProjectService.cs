using TaskFlow.Api.Dtos.Management.ProjectManagement;

namespace TaskFlow.Api.Services.Interfaces.Management.ProjectManagement;

public interface IProjectService
{
    Task<IReadOnlyCollection<ProjectWithCountDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<ProjectDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProjectEmployeeDto>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>The project's team with positions and percentages. Null when the project does not exist.</summary>
    Task<IReadOnlyCollection<ProjectMemberDto>?> GetMembersAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>Replaces the project's team through the allocation rules (all-or-nothing, 100% cap per person). Returns null
    /// when the project does not exist; throws <see cref="InvalidOperationException"/> when a rule is broken.</summary>
    Task<IReadOnlyCollection<ProjectMemberDto>?> ReplaceMembersAsync(int projectId, ReplaceProjectMembersRequest request, CancellationToken cancellationToken);

    /// <summary>The project's tasks, filtered, soonest deadline first. Null when the project does not exist.</summary>
    Task<IReadOnlyCollection<ProjectTaskDto>?> GetTasksAsync(int projectId, ProjectTaskFilter filter, CancellationToken cancellationToken);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
