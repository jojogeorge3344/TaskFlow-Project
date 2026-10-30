using TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Management.ProjectManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Interfaces;
using TaskFlow.Api.Repositories.Interfaces.Management.ProjectManagement;
using TaskFlow.Api.Services.Interfaces.Management.ProjectManagement;

namespace TaskFlow.Api.Services.Management.ProjectManagement;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IUserAllocationService _allocations;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectService(IProjectRepository projects, IUserAllocationService allocations, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _allocations = allocations;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<ProjectWithCountDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await _projects.GetAllWithCountsAsync(cancellationToken);

    public async Task<ProjectDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _projects.GetAsync(id, cancellationToken) is { } project ? ToDto(project) : null;

    public async Task<IReadOnlyCollection<ProjectEmployeeDto>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken) =>
        await _projects.GetEmployeesAsync(projectId, cancellationToken);

    public async Task<IReadOnlyCollection<ProjectMemberDto>?> GetMembersAsync(int projectId, CancellationToken cancellationToken)
    {
        if (!await _projects.ExistsActiveAsync(projectId, cancellationToken))
        {
            return null;
        }

        return await _projects.GetMembersAsync(projectId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectTaskDto>?> GetTasksAsync(
        int projectId,
        ProjectTaskFilter filter,
        CancellationToken cancellationToken)
    {
        if (!await _projects.ExistsActiveAsync(projectId, cancellationToken))
        {
            return null;
        }

        return await _projects.GetTasksAsync(projectId, filter, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectMemberDto>?> ReplaceMembersAsync(
        int projectId,
        ReplaceProjectMembersRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _projects.ExistsActiveAsync(projectId, cancellationToken))
        {
            return null;
        }

        await _allocations.ReplaceProjectMembersAsync(projectId, request.Members ?? [], cancellationToken);
        return await GetMembersAsync(projectId, cancellationToken);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _projects.NameTakenAsync(name, exceptProjectId: null, cancellationToken))
        {
            throw new BusinessRuleException("Project name already exists.");
        }

        var project = new Project
        {
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _projects.Add(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<ProjectDto?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetActiveForUpdateAsync(id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (await _projects.NameTakenAsync(name, id, cancellationToken))
        {
            throw new BusinessRuleException("Project name already exists.");
        }

        project.Name = name;
        project.Description = request.Description?.Trim();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetActiveForUpdateAsync(id, cancellationToken);
        if (project is null)
        {
            return false;
        }

        // The project is only flagged, so the allocations and manager links to it would not cascade away on their own.
        await _projects.RemoveTeamAsync(id, cancellationToken);

        project.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProjectDto ToDto(Project project) => new(project.Id, project.Name, project.Description, project.CreatedAt);
}
