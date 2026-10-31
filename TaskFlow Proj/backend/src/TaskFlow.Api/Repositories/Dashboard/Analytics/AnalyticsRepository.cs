using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Persistence;
using static TaskFlow.Api.Repositories.Dashboard.Analytics.AnalyticsCounting;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

public class AnalyticsRepository : BaseRepository<TaskEntity>, IAnalyticsRepository
{
    private const int NoKey = 0;

    public AnalyticsRepository(AppDbContext db)
        : base(db)
    {
    }

    /// <summary>The counted tasks the set describes.</summary>
    private IQueryable<TaskEntity> Build(AnalyticsTaskSet set)
    {
        var tasks = Set.AsNoTracking().Where(TaskBucketRules.IsCounted());

        if (set.TeamUserIds is { } team)
        {
            tasks = tasks.Where(task =>
                team.Contains(task.AssignedToUserId)
                || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && team.Contains(recipient.UserId)));
        }

        if (set.OwnUserId is int own)
        {
            tasks = tasks.Where(task =>
                task.AssignedToUserId == own
                || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && recipient.UserId == own));
        }

        if (set.ActiveMembersOfDepartmentId is int department)
        {
            var members = Db.Users.Where(user =>
                user.IsActive && user.DepartmentMemberships.Any(membership => membership.DepartmentId == department));
            tasks = tasks.Where(task => members.Any(member => member.Id == task.AssignedToUserId));
        }

        tasks = tasks.ApplyEntityFilters(Db, set.DepartmentId, set.ProjectId, set.EmployeeId, set.ManagerId);

        return set.Dates is { } dates
            ? tasks.ApplyDateRange(dates.Basis, dates.FromUtc, dates.ToUtcExclusive)
            : tasks;
    }

    // ---------------------------------------------------------------- task sets

    public async Task<IReadOnlyList<int>> GetTaskIdsAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default) =>
        await Build(set).Select(task => task.Id).ToListAsync(cancellationToken);

    public Task<int> CountTasksAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default) =>
        Build(set).CountAsync(cancellationToken);

    public Task<BucketCountsDto> CountByStatusAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default) =>
        TotalsAsync(Build(set), now, cancellationToken);

    public Task<IReadOnlyList<PriorityCountDto>> CountByPriorityAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default) =>
        ByPriorityAsync(Build(set), cancellationToken);

    public async Task<DeadlineBucketsDto> CountByDeadlineAsync(AnalyticsTaskSet set, DeadlineBounds bounds, CancellationToken cancellationToken = default)
    {
        var tasks = Build(set);
        return new DeadlineBucketsDto(
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Overdue, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.DueToday, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Next7Days, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Later, bounds), cancellationToken));
    }

    public async Task<IReadOnlyList<QuarterHourCount>> CountCreatedPerQuarterHourAsync(
        AnalyticsTaskSet set,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default) =>
        await QuarterHourCountsAsync(
            Build(set)
                .Where(task => task.CreatedAt >= fromUtc && task.CreatedAt < toUtcExclusive)
                .Select(task => task.CreatedAt),
            cancellationToken);

    public async Task<IReadOnlyList<QuarterHourCount>> CountCompletedPerQuarterHourAsync(
        AnalyticsTaskSet set,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        bool completedStatusOnly,
        CancellationToken cancellationToken = default)
    {
        var tasks = Build(set);
        if (completedStatusOnly)
        {
            tasks = tasks.Where(task => task.Status == TaskStatus.Completed);
        }

        return await QuarterHourCountsAsync(
            tasks
                .Where(task => task.CompletedAt != null && task.CompletedAt >= fromUtc && task.CompletedAt < toUtcExclusive)
                .Select(task => task.CompletedAt!.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByDepartmentAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default)
    {
        // A person in several departments counts in each of them; a person in none falls in the unassigned bucket.
        var memberships = Db.UserDepartments.IgnoreQueryFilters();
        return await CountByAsync(
            Build(set),
            now,
            query => query
                .SelectMany(
                    task => memberships
                        .Where(membership => membership.UserId == task.AssignedToUserId)
                        .Select(membership => (int?)membership.DepartmentId)
                        .DefaultIfEmpty(),
                    (task, departmentId) => departmentId ?? NoKey)
                .GroupBy(departmentId => departmentId)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByPersonAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default)
    {
        var recipients = Db.TaskRecipients;
        return await CountByAsync(
            Build(set),
            now,
            query =>
            {
                var assignees = query.Select(task => new { TaskId = task.Id, UserId = task.AssignedToUserId });
                var shared = query.SelectMany(task => recipients
                    .Where(recipient => recipient.TaskId == task.Id)
                    .Select(recipient => new { TaskId = recipient.TaskId, UserId = recipient.UserId }));
                return assignees
                    .Union(shared)
                    .GroupBy(pair => pair.UserId)
                    .Select(group => new KeyCount(group.Key, group.Count()));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByDepartmentManagerAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default)
    {
        var pairs = DepartmentManagerTeams();
        return await CountByAsync(
            Build(set),
            now,
            query => query
                .Join(pairs, task => task.AssignedToUserId, pair => pair.UserId, (task, pair) => pair.ManagerId)
                .GroupBy(managerId => managerId)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByProjectAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default) =>
        await CountByAsync(
            Build(set),
            now,
            query => query
                .GroupBy(task => task.ProjectId ?? NoKey)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);

    public async Task<IReadOnlyDictionary<int, DateTime>> GetLatestDeadlineByProjectAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default) =>
        (await Build(set)
            .GroupBy(task => task.ProjectId ?? NoKey)
            .Select(group => new { group.Key, Latest = group.Max(task => task.Deadline) })
            .ToListAsync(cancellationToken))
        .ToDictionary(row => row.Key, row => row.Latest);

    public async Task<IReadOnlyList<OverdueTaskRow>> GetOverdueAsync(AnalyticsTaskSet set, DateTime now, int take, CancellationToken cancellationToken = default) =>
        await Build(set)
            .Where(TaskBucketRules.InBucket(TaskBucket.Overdue, now))
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(take)
            .Select(task => new OverdueTaskRow(task.Id, task.TaskCode, task.Title, task.Deadline, task.AssignedToUserId, task.ProjectId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeadlineTaskRow>> GetOpenDueBeforeAsync(AnalyticsTaskSet set, DateTime beforeUtc, int take, CancellationToken cancellationToken = default) =>
        await Build(set)
            .Where(task => task.Status != TaskStatus.Completed && task.Deadline < beforeUtc)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(take)
            .Select(task => new DeadlineTaskRow(task.Id, task.TaskCode, task.Title, task.Priority, task.Deadline))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectProgressRow>> GetProjectProgressAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default) =>
        await Build(set)
            .Where(task => task.ProjectId != null)
            .GroupBy(task => task.ProjectId!.Value)
            .Select(group => new ProjectProgressRow(
                group.Key,
                group.Count(),
                group.Count(task => task.Status == TaskStatus.Completed),
                group.Where(task => task.Status != TaskStatus.Completed).Max(task => (DateTime?)task.Deadline)))
            .ToListAsync(cancellationToken);

    // ---------------------------------------------------------------- people, departments and projects

    public async Task<IReadOnlyList<AnalyticsOptionDto>> GetDepartmentsAsync(int? only, CancellationToken cancellationToken = default)
    {
        var departments = Db.Departments.AsQueryable();
        if (only is int id)
        {
            departments = departments.Where(department => department.Id == id);
        }

        return await departments
            .OrderBy(department => department.Name)
            .Select(department => new AnalyticsOptionDto(department.Id, department.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalyticsOptionDto>> GetProjectsAsync(int? only, IReadOnlyCollection<int>? within, CancellationToken cancellationToken = default)
    {
        var projects = Db.Projects.AsQueryable();
        if (within is not null)
        {
            projects = projects.Where(project => within.Contains(project.Id));
        }

        if (only is int id)
        {
            projects = projects.Where(project => project.Id == id);
        }

        return await projects
            .OrderBy(project => project.Name)
            .Select(project => new AnalyticsOptionDto(project.Id, project.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalyticsOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await Db.Users
            .OrderBy(user => user.FullName)
            .Select(user => new AnalyticsOptionDto(user.Id, user.FullName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AnalyticsOptionDto>> GetManagersAsync(int? only, CancellationToken cancellationToken = default)
    {
        var managers = Db.Users.Where(user => user.RoleId == SystemRoles.ManagerId);
        if (only is int id)
        {
            managers = managers.Where(user => user.Id == id);
        }

        return await managers
            .OrderBy(user => user.FullName)
            .Select(user => new AnalyticsOptionDto(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountEmployeesAsync(
        string viewAllPermissionKey,
        int? employeeId,
        int? departmentId,
        int? managerId,
        CancellationToken cancellationToken = default)
    {
        var employees = Db.Users.Where(user =>
            user.IsActive
            && !user.Role.Permissions.Any(grant => grant.Permission.Key == viewAllPermissionKey));

        if (employeeId is int onlyEmployee)
        {
            employees = employees.Where(user => user.Id == onlyEmployee);
        }

        if (departmentId is int onlyDepartment)
        {
            employees = employees.Where(user => user.DepartmentMemberships.Any(m => m.DepartmentId == onlyDepartment));
        }

        if (managerId is int onlyManager)
        {
            var team = DashboardTaskFilters.TeamUserIds(Db, onlyManager);
            employees = employees.Where(user => team.Contains(user.Id));
        }

        return employees.CountAsync(cancellationToken);
    }

    public Task<int> CountManagersAsync(int? only, CancellationToken cancellationToken = default) =>
        Db.Users.CountAsync(user => user.RoleId == SystemRoles.ManagerId && (only == null || user.Id == only), cancellationToken);

    public Task<int> CountDepartmentsAsync(int? only, CancellationToken cancellationToken = default) =>
        Db.Departments.CountAsync(department => only == null || department.Id == only, cancellationToken);

    public Task<int> CountProjectsAsync(int? only, CancellationToken cancellationToken = default) =>
        Db.Projects.CountAsync(project => only == null || project.Id == only, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetDepartmentTeamSizesAsync(CancellationToken cancellationToken = default) =>
        (await DepartmentManagerTeams()
            .GroupBy(pair => pair.ManagerId)
            .Select(group => new KeyCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken))
        .ToDictionary(row => row.Key, row => row.Count);

    public async Task<IReadOnlyDictionary<int, int>> GetManagedDepartmentCountsAsync(CancellationToken cancellationToken = default) =>
        (await Db.DepartmentManagers
            .GroupBy(link => link.ManagerUserId)
            .Select(group => new KeyCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken))
        .ToDictionary(row => row.Key, row => row.Count);

    public async Task<IReadOnlyDictionary<int, int>> GetManagedProjectCountsAsync(CancellationToken cancellationToken = default) =>
        (await Db.ManagerProjects
            .GroupBy(link => link.ManagerUserId)
            .Select(group => new KeyCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken))
        .ToDictionary(row => row.Key, row => row.Count);

    public async Task<IReadOnlyDictionary<int, string>> GetUserNamesIncludingDeletedAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Db.Users
            .IgnoreQueryFilters()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Db.Users
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetProjectNamesIncludingDeletedAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Db.Projects
            .IgnoreQueryFilters()
            .Where(project => ids.Contains(project.Id))
            .Select(project => new { project.Id, project.Name })
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

    public Task<bool> DepartmentExistsAsync(int id, CancellationToken cancellationToken = default) =>
        Db.Departments.AnyAsync(department => department.Id == id, cancellationToken);

    public Task<bool> ProjectExistsAsync(int id, CancellationToken cancellationToken = default) =>
        Db.Projects.AnyAsync(project => project.Id == id, cancellationToken);

    public Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken = default) =>
        Db.Users.AnyAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyList<int>> GetManagerProjectIdsAsync(int managerId, IReadOnlyList<int> teamUserIds, CancellationToken cancellationToken = default)
    {
        var assigned = Db.ManagerProjects
            .Where(link => link.ManagerUserId == managerId)
            .Select(link => link.ProjectId);
        var ofTeamTasks = Set
            .Where(TaskBucketRules.IsCounted())
            .Where(task =>
                teamUserIds.Contains(task.AssignedToUserId)
                || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && teamUserIds.Contains(recipient.UserId)))
            .Where(task => task.ProjectId != null)
            .Select(task => task.ProjectId!.Value);

        return await Db.Projects
            .Where(project => assigned.Contains(project.Id) || ofTeamTasks.Contains(project.Id))
            .Select(project => project.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<int?> GetFirstDepartmentIdAsync(int userId, CancellationToken cancellationToken = default) =>
        Db.UserDepartments
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Department.Name)
            .Select(membership => (int?)membership.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetDepartmentNameAsync(int departmentId, CancellationToken cancellationToken = default) =>
        Db.Departments
            .Where(department => department.Id == departmentId)
            .Select(department => department.Name)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountActiveDepartmentMembersAsync(int departmentId, CancellationToken cancellationToken = default) =>
        Db.Users.CountAsync(
            user => user.IsActive && user.DepartmentMemberships.Any(membership => membership.DepartmentId == departmentId),
            cancellationToken);

    public async Task<IReadOnlyList<int>> GetAllocatedProjectIdsAsync(int userId, CancellationToken cancellationToken = default) =>
        await Db.UserProjectAllocations
            .Where(allocation => allocation.UserId == userId)
            .Select(allocation => allocation.ProjectId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AnalyticsOptionDto>> GetProjectsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Db.Projects
            .Where(project => ids.Contains(project.Id))
            .Select(project => new AnalyticsOptionDto(project.Id, project.Name))
            .ToListAsync(cancellationToken);

    /// <summary>A department manager's team: everyone in the departments they manage.</summary>
    private IQueryable<ManagerMember> DepartmentManagerTeams() =>
        Db.DepartmentManagers
            .Join(
                Db.UserDepartments,
                link => link.DepartmentId,
                membership => membership.DepartmentId,
                (link, membership) => new ManagerMember { ManagerId = link.ManagerUserId, UserId = membership.UserId })
            .Distinct();

    /// <summary>Member-initialised (not positional) so EF can join and group on it.</summary>
    private sealed class ManagerMember
    {
        public int ManagerId { get; init; }

        public int UserId { get; init; }
    }
}
