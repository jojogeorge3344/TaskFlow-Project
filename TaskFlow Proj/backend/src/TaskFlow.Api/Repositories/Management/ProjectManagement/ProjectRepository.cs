using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Management.ProjectManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Management.ProjectManagement;

public class ProjectRepository : BaseRepository<Project>, IProjectRepository
{
    public ProjectRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<ProjectWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Select(p => new ProjectWithCountDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                Db.UserProjectAllocations.Count(a => a.ProjectId == p.Id),
                Db.UserProjectAllocations
                    .Where(a => a.ProjectId == p.Id && a.IsProjectManager)
                    .OrderBy(a => a.User.FullName)
                    .Select(a => a.User.FullName)
                    .ToList()))
            .ToListAsync(cancellationToken);

    public Task<Project?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Project?> GetActiveForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<bool> ExistsActiveAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<int> CountActiveAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        Set.CountAsync(p => ids.Contains(p.Id) && !p.IsDeleted, cancellationToken);

    public Task<bool> NameTakenAsync(string name, int? exceptProjectId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(p => p.Id != exceptProjectId && p.Name.ToLower() == name.ToLower() && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<ProjectEmployeeDto>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken = default) =>
        await Db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.ProjectAllocations.Any(a => a.ProjectId == projectId))
            .Select(u => new ProjectEmployeeDto(
                u.Id, u.EmployeeCode, u.FullName, u.Username, u.Email, u.MobileNumber, u.RoleId, u.IsActive, u.CreatedAt, u.UpdatedAt, u.LastLoginAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default) =>
        await Db.UserProjectAllocations.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.User.FullName)
            .Select(a => new ProjectMemberDto(
                a.UserId, a.User.FullName, a.User.Email, a.PositionId, a.Position.Name, a.AllocationPercent, a.IsProjectManager,
                a.User.RoleId == SystemRoles.ManagerId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTaskDto>> GetTasksAsync(
        int projectId,
        ProjectTaskFilter filter,
        CancellationToken cancellationToken = default)
    {
        var tasks = Db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && !t.IsDeleted);
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
            tasks = tasks.Where(t => Db.UserProjectAllocations.Any(a =>
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
                Db.UserProjectAllocations
                    .Where(a => a.ProjectId == projectId && a.UserId == t.AssignedToUserId)
                    .Select(a => (int?)a.PositionId)
                    .FirstOrDefault(),
                Db.UserProjectAllocations
                    .Where(a => a.ProjectId == projectId && a.UserId == t.AssignedToUserId)
                    .Select(a => a.Position.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task RemoveTeamAsync(int projectId, CancellationToken cancellationToken = default)
    {
        Db.UserProjectAllocations.RemoveRange(
            await Db.UserProjectAllocations.IgnoreQueryFilters().Where(a => a.ProjectId == projectId).ToListAsync(cancellationToken));
        Db.ManagerProjects.RemoveRange(
            await Db.ManagerProjects.Where(link => link.ProjectId == projectId).ToListAsync(cancellationToken));
    }
}
