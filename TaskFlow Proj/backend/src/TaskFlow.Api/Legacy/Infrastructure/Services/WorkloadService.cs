using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Users;
using TaskFlow.Application.Workload;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Per-employee workload aggregation (Screen 16). Covers every active employee
/// (every user whose role does not hold <c>tasks.view.all</c>, which is the Employee role by default), organization-wide for a Manager/Administrator, matching the
/// dashboards' scoping (see the change design, D3).</summary>
public class WorkloadService : IWorkloadService
{
    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;

    public WorkloadService(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<EmployeeWorkloadDto>> GetWorkloadAsync(
        int? projectId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var usersQuery = _dbContext.Users.Where(user =>
            user.IsActive
            && !user.Role.Permissions.Any(grant => grant.Permission.Key == PermissionKeys.TasksViewAll));
        if (projectId is not null)
        {
            usersQuery = usersQuery.Where(user => user.ProjectAllocations.Any(a => a.ProjectId == projectId));
        }

        var users = await usersQuery
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                Projects = user.ProjectAllocations
                    .OrderBy(a => a.Project.Name)
                    .Select(a => new ProjectAllocationDto(a.ProjectId, a.Project.Name, a.PositionId, a.Position.Name, a.AllocationPercent))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return Array.Empty<EmployeeWorkloadDto>();
        }

        var userIds = users.Select(user => user.Id).ToList();

        var counts = await _dbContext.Tasks
            .Where(task => !task.IsDeleted && userIds.Contains(task.AssignedToUserId))
            .GroupBy(task => task.AssignedToUserId)
            .Select(group => new WorkloadCounts(
                group.Key,
                group.Count(task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled),
                group.Count(task =>
                    task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled &&
                    (task.Priority == TaskPriority.High || task.Priority == TaskPriority.Critical)),
                group.Count(task =>
                    task.Deadline < now && task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled),
                group.Count(task => task.Status == TaskStatus.Completed)))
            .ToListAsync(cancellationToken);

        var byUser = counts.ToDictionary(entry => entry.UserId);

        return users
            .Select(user =>
            {
                byUser.TryGetValue(user.Id, out var count);
                return new EmployeeWorkloadDto(
                    user.Id,
                    user.FullName,
                    user.Projects,
                    user.Projects.Sum(project => project.Percent),
                    count?.Active ?? 0,
                    count?.HighPriority ?? 0,
                    count?.Overdue ?? 0,
                    count?.Completed ?? 0);
            })
            .ToList();
    }

    private sealed record WorkloadCounts(int UserId, int Active, int HighPriority, int Overdue, int Completed);
}
