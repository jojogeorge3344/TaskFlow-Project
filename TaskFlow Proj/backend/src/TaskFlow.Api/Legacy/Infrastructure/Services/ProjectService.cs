using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _dbContext;
    private readonly IUserAllocationService _allocations;

    public ProjectService(AppDbContext dbContext, IUserAllocationService allocations)
    {
        _dbContext = dbContext;
        _allocations = allocations;
    }

    public async Task<IReadOnlyCollection<ProjectWithCountDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Select(p => new ProjectWithCountDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                _dbContext.UserProjectAllocations.Count(a => a.ProjectId == p.Id),
                _dbContext.UserProjectAllocations
                    .Where(a => a.ProjectId == p.Id && a.IsProjectManager)
                    .OrderBy(a => a.User.FullName)
                    .Select(a => a.User.FullName)
                    .ToList()))
            .ToListAsync(cancellationToken);
        return projects;
    }

    public async Task<ProjectDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.ProjectAllocations.Any(a => a.ProjectId == projectId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectMemberDto>?> GetMembersAsync(int projectId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken))
        {
            return null;
        }

        return await _dbContext.UserProjectAllocations
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.User.FullName)
            .Select(a => new ProjectMemberDto(a.UserId, a.User.FullName, a.User.Email, a.PositionId, a.Position.Name, a.AllocationPercent, a.IsProjectManager, a.User.RoleId == SystemRoles.ManagerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectTaskDto>?> GetTasksAsync(
        int projectId,
        ProjectTaskFilter filter,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken))
        {
            return null;
        }

        var tasks = _dbContext.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && !t.IsDeleted);
        if (filter.Status is not null)
        {
            tasks = tasks.Where(t => t.Status == filter.Status);
        }
        if (filter.Priority is not null)
        {
            tasks = tasks.Where(t => t.Priority == filter.Priority);
        }
        if (filter.DeadlineFrom is not null)
        {
            tasks = tasks.Where(t => t.Deadline >= filter.DeadlineFrom);
        }
        if (filter.DeadlineTo is not null)
        {
            tasks = tasks.Where(t => t.Deadline <= filter.DeadlineTo);
        }
        if (filter.AssignedToUserId is not null)
        {
            tasks = tasks.Where(t => t.AssignedToUserId == filter.AssignedToUserId);
        }
        if (filter.PositionId is not null)
        {
            tasks = tasks.Where(t => _dbContext.UserProjectAllocations.Any(a =>
                a.ProjectId == projectId && a.UserId == t.AssignedToUserId && a.PositionId == filter.PositionId));
        }

        return await tasks
            .OrderBy(t => t.Deadline)
            .ThenBy(t => t.Id)
            .Select(t => new ProjectTaskDto(
                t.Id,
                t.TaskCode,
                t.Title,
                t.Status,
                t.Priority,
                t.StartDate,
                t.Deadline,
                t.AssignedToUserId,
                t.AssignedTo!.FullName,
                _dbContext.UserProjectAllocations
                    .Where(a => a.ProjectId == projectId && a.UserId == t.AssignedToUserId)
                    .Select(a => (int?)a.PositionId)
                    .FirstOrDefault(),
                _dbContext.UserProjectAllocations
                    .Where(a => a.ProjectId == projectId && a.UserId == t.AssignedToUserId)
                    .Select(a => a.Position.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectMemberDto>?> ReplaceMembersAsync(
        int projectId,
        ReplaceProjectMembersRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken))
        {
            return null;
        }

        await _allocations.ReplaceProjectMembersAsync(projectId, request.Members ?? [], cancellationToken);
        return await GetMembersAsync(projectId, cancellationToken);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var exists = await _dbContext.Projects
            .AnyAsync(p => p.Name.ToLower() == name.ToLower() && !p.IsDeleted, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Project name already exists.");
        }

        var project = new Project
        {
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt);
    }

    public async Task<ProjectDto?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        var exists = await _dbContext.Projects
            .AnyAsync(p => p.Id != id && p.Name.ToLower() == name.ToLower() && !p.IsDeleted, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Project name already exists.");
        }

        project.Name = name;
        project.Description = request.Description?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null)
        {
            return false;
        }

        // The project is only flagged, so the allocations and manager links to it would not cascade away on their own.
        _dbContext.UserProjectAllocations.RemoveRange(
            await _dbContext.UserProjectAllocations.IgnoreQueryFilters().Where(a => a.ProjectId == id).ToListAsync(cancellationToken));
        _dbContext.ManagerProjects.RemoveRange(
            await _dbContext.ManagerProjects.Where(link => link.ProjectId == id).ToListAsync(cancellationToken));

        project.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
