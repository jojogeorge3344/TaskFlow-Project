using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;

public class WorkloadRepository : BaseRepository<User>, IWorkloadRepository
{
    public WorkloadRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<WorkloadEmployee>> GetEmployeesAsync(
        string viewAllPermissionKey,
        int? projectId,
        CancellationToken cancellationToken = default)
    {
        var users = Set.Where(user =>
            user.IsActive
            && !user.Role.Permissions.Any(grant => grant.Permission.Key == viewAllPermissionKey));

        if (projectId is not null)
        {
            users = users.Where(user => user.ProjectAllocations.Any(a => a.ProjectId == projectId));
        }

        return await users
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Select(user => new WorkloadEmployee(
                user.Id,
                user.FullName,
                user.ProjectAllocations
                    .OrderBy(a => a.Project.Name)
                    .Select(a => new ProjectAllocationDto(a.ProjectId, a.Project.Name, a.PositionId, a.Position.Name, a.AllocationPercent))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkloadCounts>> GetTaskCountsAsync(
        IReadOnlyCollection<int> userIds,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        await Db.Tasks
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
}
