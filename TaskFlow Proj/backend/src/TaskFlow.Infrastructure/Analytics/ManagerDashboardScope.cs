using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Rules;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Infrastructure.Analytics;

public class ManagerDashboardScope : IManagerDashboardScope
{
    private readonly AppDbContext _dbContext;
    private readonly AnalyticsFilterResolver _resolver;

    public ManagerDashboardScope(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _resolver = new AnalyticsFilterResolver(dbContext, clock);
    }

    public IQueryable<int> TeamUserIds(int managerId)
    {
        var managedDepartments = _dbContext.DepartmentManagers
            .Where(link => link.ManagerUserId == managerId)
            .Select(link => link.DepartmentId);

        return _dbContext.Users
            .Where(user => user.IsActive
                && (user.ManagerId == managerId
                    || (user.DepartmentId != null && managedDepartments.Contains(user.DepartmentId.Value))))
            .Select(user => user.Id);
    }

    public IQueryable<int> ProjectIds(int managerId)
    {
        var team = TeamUserIds(managerId);
        var assigned = _dbContext.ManagerProjects
            .Where(link => link.ManagerUserId == managerId)
            .Select(link => link.ProjectId);
        var ofTeamTasks = TeamTasks(_dbContext.Tasks.Where(TaskBucketRules.IsCounted()), team)
            .Where(task => task.ProjectId != null)
            .Select(task => task.ProjectId!.Value);

        return _dbContext.Projects
            .Where(project => assigned.Contains(project.Id) || ofTeamTasks.Contains(project.Id))
            .Select(project => project.Id);
    }

    public async Task<ScopedTasks> ScopeAsync(int managerId, AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        // The department and manager filters are not part of the Manager contract; they are dropped before resolving.
        var requested = filter with { DepartmentId = null, ManagerId = null };
        await EnsureInScopeAsync(managerId, requested, cancellationToken);
        var resolved = await _resolver.ResolveAsync(requested, checkEntitiesExist: false, cancellationToken);

        // The team is read once and passed to the task queries as a plain id list, so the many aggregate queries a
        // dashboard request runs do not each re-evaluate the department and report subqueries.
        var team = await TeamUserIds(managerId).ToListAsync(cancellationToken);
        var withoutDates = TeamTasks(_dbContext.Tasks.AsNoTracking().Where(TaskBucketRules.IsCounted()), team)
            .ApplyEntityFilters(_dbContext, null, resolved.ProjectId, resolved.EmployeeId, null);
        var tasks = withoutDates.ApplyDateRange(resolved.Basis, resolved.FromUtc, resolved.ToUtcExclusive);

        return new ScopedTasks(tasks, withoutDates, resolved);
    }

    public IQueryable<TaskEntity> TeamTasks(IQueryable<TaskEntity> tasks, int managerId) =>
        TeamTasks(tasks, TeamUserIds(managerId));

    private IQueryable<TaskEntity> TeamTasks(IQueryable<TaskEntity> tasks, List<int> team) =>
        tasks.Where(task =>
            team.Contains(task.AssignedToUserId)
            || _dbContext.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && team.Contains(recipient.UserId)));

    private IQueryable<TaskEntity> TeamTasks(IQueryable<TaskEntity> tasks, IQueryable<int> team) =>
        tasks.Where(task =>
            team.Contains(task.AssignedToUserId)
            || _dbContext.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && team.Contains(recipient.UserId)));

    private async System.Threading.Tasks.Task EnsureInScopeAsync(
        int managerId,
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.EmployeeId is int employeeId)
        {
            if (!await TeamUserIds(managerId).ContainsAsync(employeeId, cancellationToken))
            {
                var exists = await _dbContext.Users.AnyAsync(user => user.Id == employeeId, cancellationToken);
                throw exists
                    ? new AnalyticsForbiddenException("The employee is outside your team.")
                    : new AnalyticsEntityNotFoundException("The employee does not exist.");
            }
        }

        if (filter.ProjectId is int projectId)
        {
            if (!await ProjectIds(managerId).ContainsAsync(projectId, cancellationToken))
            {
                var exists = await _dbContext.Projects.AnyAsync(project => project.Id == projectId, cancellationToken);
                throw exists
                    ? new AnalyticsForbiddenException("The project is outside your scope.")
                    : new AnalyticsEntityNotFoundException("The project does not exist.");
            }
        }
    }
}
