using TaskFlow.Api.Repositories.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using static TaskFlow.Infrastructure.Analytics.AnalyticsCounting;

namespace TaskFlow.Infrastructure.Analytics;

public class AdminAnalyticsService : IAdminAnalyticsService
{
    private const string NoDepartment = "No department";
    private const string NoProject = "No project";
    private const int TopEmployees = 10;
    private const int DailyTrendMaxDays = 31;
    private const int NoKey = 0;

    private readonly AppDbContext _dbContext;
    private readonly IDashboardScope _scope;
    private readonly IAppClock _clock;

    public AdminAnalyticsService(AppDbContext dbContext, IDashboardScope scope, IAppClock clock)
    {
        _dbContext = dbContext;
        _scope = scope;
        _clock = clock;
    }

    public async Task<AdminAnalyticsDto> GetAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var scoped = await _scope.ScopeAsync(filter, cancellationToken);
        var resolved = scoped.Filter;
        var now = resolved.NowUtc;

        var byStatus = await TotalsAsync(scoped.Tasks, now, cancellationToken);

        return new AdminAnalyticsDto(
            new AppliedFilterDto(
                resolved.From,
                resolved.To,
                resolved.Basis,
                resolved.DepartmentId,
                resolved.ProjectId,
                resolved.EmployeeId,
                resolved.ManagerId),
            await SummaryAsync(resolved, byStatus, cancellationToken),
            byStatus,
            await ByPriorityAsync(scoped.Tasks, cancellationToken),
            await DeadlineBucketsAsync(scoped.Tasks, resolved, cancellationToken),
            await TrendAsync(scoped.TasksIgnoringDates, resolved, cancellationToken),
            await ByDepartmentAsync(scoped.Tasks, resolved, cancellationToken),
            await ByEmployeeAsync(scoped.Tasks, now, resolved.AllEmployees, cancellationToken),
            await ByManagerAsync(scoped.Tasks, resolved, cancellationToken),
            await ByProjectAsync(scoped.Tasks, resolved, cancellationToken));
    }

    public async Task<AnalyticsFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var today = ReminderCalculator.LocalDate(_clock.UtcNow, timeZone);

        var departments = await _dbContext.Departments
            .OrderBy(department => department.Name)
            .Select(department => new AnalyticsOptionDto(department.Id, department.Name))
            .ToListAsync(cancellationToken);
        var projects = await _dbContext.Projects
            .OrderBy(project => project.Name)
            .Select(project => new AnalyticsOptionDto(project.Id, project.Name))
            .ToListAsync(cancellationToken);
        var employees = await _dbContext.Users
            .OrderBy(user => user.FullName)
            .Select(user => new AnalyticsOptionDto(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
        var managers = await ManagerUsers()
            .OrderBy(user => user.FullName)
            .Select(user => new AnalyticsOptionDto(user.Id, user.FullName))
            .ToListAsync(cancellationToken);

        return new AnalyticsFilterOptionsDto(today, timeZone.Id, departments, projects, employees, managers);
    }

    private IQueryable<TaskFlow.Api.Models.Entities.User> ManagerUsers() =>
        _dbContext.Users.Where(user => user.RoleId == SystemRoles.ManagerId);

    private IQueryable<TaskFlow.Api.Models.Entities.User> EmployeeUsers() =>
        _dbContext.Users.Where(user =>
            user.IsActive
            && !user.Role.Permissions.Any(grant => grant.Permission.Key == PermissionKeys.TasksViewAll));

    private async Task<AnalyticsSummaryDto> SummaryAsync(
        ResolvedAnalyticsFilter filter,
        BucketCountsDto byStatus,
        CancellationToken cancellationToken)
    {
        var employees = EmployeeUsers();
        if (filter.EmployeeId is int employeeId)
        {
            employees = employees.Where(user => user.Id == employeeId);
        }

        if (filter.DepartmentId is int departmentId)
        {
            employees = employees.Where(user => user.DepartmentMemberships.Any(m => m.DepartmentId == departmentId));
        }

        if (filter.ManagerId is int managerId)
        {
            var team = DashboardTaskFilters.TeamUserIds(_dbContext, managerId);
            employees = employees.Where(user => team.Contains(user.Id));
        }

        var managers = ManagerUsers();
        if (filter.ManagerId is int onlyManager)
        {
            managers = managers.Where(user => user.Id == onlyManager);
        }

        var departments = _dbContext.Departments.AsQueryable();
        if (filter.DepartmentId is int onlyDepartment)
        {
            departments = departments.Where(department => department.Id == onlyDepartment);
        }

        var projects = _dbContext.Projects.AsQueryable();
        if (filter.ProjectId is int onlyProject)
        {
            projects = projects.Where(project => project.Id == onlyProject);
        }

        return new AnalyticsSummaryDto(
            await employees.CountAsync(cancellationToken),
            await managers.CountAsync(cancellationToken),
            await departments.CountAsync(cancellationToken),
            await projects.CountAsync(cancellationToken),
            byStatus);
    }

    private static async Task<DeadlineBucketsDto> DeadlineBucketsAsync(
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var bounds = DeadlineBucketRules.BoundsFor(filter.NowUtc, filter.TimeZone);

        return new DeadlineBucketsDto(
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Overdue, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.DueToday, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Next7Days, bounds), cancellationToken),
            await tasks.CountAsync(DeadlineBucketRules.InBucket(DeadlineBucket.Later, bounds), cancellationToken));
    }

    private static async Task<TrendDto> TrendAsync(
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var created = await QuarterHourCountsAsync(
            tasks
                .Where(task => task.CreatedAt >= filter.FromUtc && task.CreatedAt < filter.ToUtcExclusive)
                .Select(task => task.CreatedAt),
            cancellationToken);
        var completed = await QuarterHourCountsAsync(
            tasks
                .Where(task => task.CompletedAt != null
                    && task.CompletedAt >= filter.FromUtc
                    && task.CompletedAt < filter.ToUtcExclusive)
                .Select(task => task.CompletedAt!.Value),
            cancellationToken);

        var weekly = filter.To.DayNumber - filter.From.DayNumber + 1 > DailyTrendMaxDays;
        var firstBucket = weekly ? StartOfWeek(filter.From) : filter.From;
        var step = weekly ? 7 : 1;

        DateOnly BucketOf(DateTime utc)
        {
            var day = ReminderCalculator.LocalDate(utc, filter.TimeZone);
            return weekly ? StartOfWeek(day) : day;
        }

        var createdPerBucket = created.GroupBy(row => BucketOf(row.Start)).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
        var completedPerBucket = completed.GroupBy(row => BucketOf(row.Start)).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));

        var points = new List<TrendPointDto>();
        for (var day = firstBucket; day <= filter.To; day = day.AddDays(step))
        {
            points.Add(new TrendPointDto(
                day,
                createdPerBucket.GetValueOrDefault(day),
                completedPerBucket.GetValueOrDefault(day)));
        }

        return new TrendDto(weekly ? "week" : "day", points);
    }

    private static DateOnly StartOfWeek(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private async Task<IReadOnlyList<DepartmentAnalyticsDto>> ByDepartmentAsync(
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        // A person in several departments counts in each of them; a person in none falls in the unassigned bucket.
        var memberships = _dbContext.UserDepartments.IgnoreQueryFilters();
        var perDepartment = await CountByAsync(
            tasks,
            filter.NowUtc,
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

        var departments = _dbContext.Departments.AsQueryable();
        if (filter.DepartmentId is int onlyDepartment)
        {
            departments = departments.Where(department => department.Id == onlyDepartment);
        }

        var named = await departments
            .OrderBy(department => department.Name)
            .Select(department => new { department.Id, department.Name })
            .ToListAsync(cancellationToken);

        var result = named
            .Select(department => new DepartmentAnalyticsDto(
                department.Id,
                department.Name,
                perDepartment.GetValueOrDefault(department.Id, Empty)))
            .ToList();

        var knownIds = named.Select(department => department.Id).ToHashSet();
        var unassigned = Sum(perDepartment.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value));
        if (unassigned.Total > 0)
        {
            result.Add(new DepartmentAnalyticsDto(null, NoDepartment, unassigned));
        }

        return result;
    }

    private async Task<EmployeeAnalyticsSectionDto> ByEmployeeAsync(
        IQueryable<TaskEntity> tasks,
        DateTime now,
        bool all,
        CancellationToken cancellationToken)
    {
        var recipients = _dbContext.TaskRecipients;
        var perEmployee = await CountByAsync(
            tasks,
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

        var top = perEmployee
            .OrderByDescending(pair => pair.Value.Open)
            .ThenByDescending(pair => pair.Value.Total)
            .ThenBy(pair => pair.Key)
            .Take(all ? int.MaxValue : TopEmployees)
            .ToList();

        var topIds = top.Select(pair => pair.Key).ToList();
        var names = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => topIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var items = top
            .Select(pair => new EmployeeAnalyticsDto(pair.Key, names.GetValueOrDefault(pair.Key, "Unknown"), pair.Value))
            .OrderByDescending(item => item.Tasks.Open)
            .ThenByDescending(item => item.Tasks.Total)
            .ThenBy(item => item.Name)
            .ToList();

        return new EmployeeAnalyticsSectionDto(items, perEmployee.Count);
    }

    private async Task<IReadOnlyList<ManagerAnalyticsDto>> ByManagerAsync(
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var managers = ManagerUsers();
        if (filter.ManagerId is int onlyManager)
        {
            managers = managers.Where(user => user.Id == onlyManager);
        }

        var listed = await managers
            .OrderBy(user => user.FullName)
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        // A manager's team is everyone in the departments they manage.
        var pairs = _dbContext.DepartmentManagers
            .Join(
                _dbContext.UserDepartments,
                link => link.DepartmentId,
                membership => membership.DepartmentId,
                (link, membership) => new { ManagerId = link.ManagerUserId, UserId = membership.UserId })
            .Distinct();

        var teamTasks = await CountByAsync(
            tasks,
            filter.NowUtc,
            query => query
                .Join(pairs, task => task.AssignedToUserId, pair => pair.UserId, (task, pair) => pair.ManagerId)
                .GroupBy(managerId => managerId)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);

        var teamSizes = (await pairs
                .GroupBy(pair => pair.ManagerId)
                .Select(group => new KeyCount(group.Key, group.Count()))
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Key, row => row.Count);
        var departmentCounts = (await _dbContext.DepartmentManagers
                .GroupBy(link => link.ManagerUserId)
                .Select(group => new KeyCount(group.Key, group.Count()))
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Key, row => row.Count);
        var projectCounts = (await _dbContext.ManagerProjects
                .GroupBy(link => link.ManagerUserId)
                .Select(group => new KeyCount(group.Key, group.Count()))
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Key, row => row.Count);

        return listed
            .Select(manager => new ManagerAnalyticsDto(
                manager.Id,
                manager.FullName,
                departmentCounts.GetValueOrDefault(manager.Id),
                teamSizes.GetValueOrDefault(manager.Id),
                projectCounts.GetValueOrDefault(manager.Id),
                teamTasks.GetValueOrDefault(manager.Id, Empty)))
            .ToList();
    }

    private async Task<IReadOnlyList<ProjectAnalyticsDto>> ByProjectAsync(
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var perProject = await CountByAsync(
            tasks,
            filter.NowUtc,
            query => query
                .GroupBy(task => task.ProjectId ?? NoKey)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);

        var latestDeadlines = (await tasks
                .GroupBy(task => task.ProjectId ?? NoKey)
                .Select(group => new { group.Key, Latest = group.Max(task => task.Deadline) })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Key, row => row.Latest);

        var projects = _dbContext.Projects.AsQueryable();
        if (filter.ProjectId is int onlyProject)
        {
            projects = projects.Where(project => project.Id == onlyProject);
        }

        var named = await projects
            .OrderBy(project => project.Name)
            .Select(project => new { project.Id, project.Name })
            .ToListAsync(cancellationToken);

        var result = named
            .Select(project => new ProjectAnalyticsDto(
                project.Id,
                project.Name,
                perProject.GetValueOrDefault(project.Id, Empty),
                latestDeadlines.TryGetValue(project.Id, out var latest) ? latest : null))
            .ToList();

        var knownIds = named.Select(project => project.Id).ToHashSet();
        var unassigned = Sum(perProject.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value));
        if (unassigned.Total > 0)
        {
            var latest = latestDeadlines.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value).Max();
            result.Add(new ProjectAnalyticsDto(null, NoProject, unassigned, latest));
        }

        return result;
    }
}
