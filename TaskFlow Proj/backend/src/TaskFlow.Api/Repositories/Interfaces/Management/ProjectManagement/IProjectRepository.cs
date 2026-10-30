using TaskFlow.Api.Dtos.Management.ProjectManagement;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Management.ProjectManagement;

/// <summary>Projects, their team (allocations) as the project screens list it, and the tasks mapped to them.</summary>
public interface IProjectRepository : IBaseRepository<Project>
{
    /// <summary>Every project with its team size and the names of its project managers.</summary>
    Task<IReadOnlyList<ProjectWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default);

    Task<Project?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked project, unless it has been deleted.</summary>
    Task<Project?> GetActiveForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>How many of these ids are live projects.</summary>
    Task<int> CountActiveAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether another live project has this name, ignoring case.</summary>
    Task<bool> NameTakenAsync(string name, int? exceptProjectId, CancellationToken cancellationToken = default);

    /// <summary>The live accounts allocated to the project.</summary>
    Task<IReadOnlyList<ProjectEmployeeDto>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>The project's team with positions and percentages, by name.</summary>
    Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>The project's live tasks matching every given filter, soonest deadline first.</summary>
    Task<IReadOnlyList<ProjectTaskDto>> GetTasksAsync(int projectId, ProjectTaskFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Stages the removal of every allocation and manager link to the project, so a flagged project leaves none
    /// behind.</summary>
    System.Threading.Tasks.Task RemoveTeamAsync(int projectId, CancellationToken cancellationToken = default);
}
