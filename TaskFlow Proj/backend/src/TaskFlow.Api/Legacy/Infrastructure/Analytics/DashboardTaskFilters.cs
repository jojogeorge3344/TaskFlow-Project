using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Infrastructure.Analytics;

public static class DashboardTaskFilters
{
    public const int NoGroup = 0;

    public static IQueryable<TaskEntity> ApplyDateRange(
        this IQueryable<TaskEntity> tasks,
        AnalyticsDateBasis basis,
        DateTime fromUtc,
        DateTime toUtcExclusive) =>
        basis switch
        {
            AnalyticsDateBasis.Deadline => tasks.Where(task => task.Deadline >= fromUtc && task.Deadline < toUtcExclusive),
            AnalyticsDateBasis.Completed => tasks.Where(task =>
                task.CompletedAt != null && task.CompletedAt >= fromUtc && task.CompletedAt < toUtcExclusive),
            _ => tasks.Where(task => task.CreatedAt >= fromUtc && task.CreatedAt < toUtcExclusive)
        };

    public static IQueryable<TaskEntity> ApplyEntityFilters(
        this IQueryable<TaskEntity> tasks,
        AppDbContext db,
        int? departmentId,
        int? projectId,
        int? employeeId,
        int? managerId)
    {
        if (departmentId is int department)
        {
            if (department == NoGroup)
            {
                var departed = db.Users
                    .IgnoreQueryFilters()
                    .Where(user => db.Departments.Any(candidate => user.DepartmentMemberships.Any(m => m.DepartmentId == candidate.Id)))
                    .Select(user => user.Id);
                tasks = tasks.Where(task => !departed.Contains(task.AssignedToUserId));
            }
            else
            {
                var members = db.Users
                    .IgnoreQueryFilters()
                    .Where(user => user.DepartmentMemberships.Any(m => m.DepartmentId == department))
                    .Select(user => user.Id);
                tasks = tasks.Where(task => members.Contains(task.AssignedToUserId));
            }
        }

        if (projectId is int project)
        {
            tasks = project == NoGroup
                ? tasks.Where(task => task.ProjectId == null || !db.Projects.Any(candidate => candidate.Id == task.ProjectId))
                : tasks.Where(task => task.ProjectId == project);
        }

        if (employeeId is int employee)
        {
            tasks = tasks.Where(task =>
                task.AssignedToUserId == employee
                || db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && recipient.UserId == employee));
        }

        if (managerId is int manager)
        {
            var team = TeamUserIds(db, manager);
            tasks = tasks.Where(task => team.Contains(task.AssignedToUserId));
        }

        return tasks;
    }

    public static IQueryable<int> TeamUserIds(AppDbContext db, int managerId)
    {
        var managedDepartments = db.DepartmentManagers
            .Where(link => link.ManagerUserId == managerId)
            .Select(link => link.DepartmentId);

        return db.Users
            .Where(user => user.DepartmentMemberships.Any(m => managedDepartments.Contains(m.DepartmentId)))
            .Select(user => user.Id);
    }
}
