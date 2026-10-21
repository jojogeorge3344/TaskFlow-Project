using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Projects;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _dbContext;

    public ProjectService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
                _dbContext.Users.Count(u => u.ProjectId == p.Id && !u.IsDeleted)))
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
            .Where(u => u.ProjectId == projectId && !u.IsDeleted)
            .ToListAsync(cancellationToken);
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

        var hasEmployees = await _dbContext.Users
            .AnyAsync(u => u.ProjectId == id && !u.IsDeleted, cancellationToken);
        if (hasEmployees)
        {
            throw new InvalidOperationException("Project cannot be deleted because it has associated employees.");
        }

        // The project is only flagged, so the manager links to it would not cascade away on their own.
        _dbContext.ManagerProjects.RemoveRange(
            await _dbContext.ManagerProjects.Where(link => link.ProjectId == id).ToListAsync(cancellationToken));

        project.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
