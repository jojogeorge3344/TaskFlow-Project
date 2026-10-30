using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;
using TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public class ManagerDashboardScope : IManagerDashboardScope
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IEmployeeRepository _employees;
    private readonly AnalyticsFilterResolver _resolver;

    public ManagerDashboardScope(IAnalyticsRepository analytics, IEmployeeRepository employees, IAppClock clock)
    {
        _analytics = analytics;
        _employees = employees;
        _resolver = new AnalyticsFilterResolver(analytics, clock);
    }

    // The scope lives for one request, and a dashboard request asks for the same team and projects several times; each is
    // read once per manager.
    private readonly Dictionary<int, IReadOnlyList<int>> _teams = new();
    private readonly Dictionary<int, IReadOnlyList<int>> _projects = new();

    public async Task<IReadOnlyList<int>> TeamUserIdsAsync(int managerId, CancellationToken cancellationToken = default)
    {
        if (!_teams.TryGetValue(managerId, out var team))
        {
            team = await _employees.GetTeamMemberIdsAsync(managerId, cancellationToken);
            _teams[managerId] = team;
        }

        return team;
    }

    public async Task<IReadOnlyList<int>> ProjectIdsAsync(int managerId, CancellationToken cancellationToken = default)
    {
        if (!_projects.TryGetValue(managerId, out var projects))
        {
            projects = await _analytics.GetManagerProjectIdsAsync(managerId, await TeamUserIdsAsync(managerId, cancellationToken), cancellationToken);
            _projects[managerId] = projects;
        }

        return projects;
    }

    public async Task<ScopedTasks> ScopeAsync(int managerId, AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        // The department and manager filters are not part of the Manager contract; they are dropped before resolving.
        var requested = filter with { DepartmentId = null, ManagerId = null };
        await EnsureInScopeAsync(managerId, requested, cancellationToken);
        var resolved = await _resolver.ResolveAsync(requested, checkEntitiesExist: false, cancellationToken);

        // The team is read once and passed to the task queries as a plain id list, so the many aggregate queries a
        // dashboard request runs do not each re-evaluate the department and report subqueries.
        var team = await TeamUserIdsAsync(managerId, cancellationToken);
        var withoutDates = new AnalyticsTaskSet(
            TeamUserIds: team,
            ProjectId: resolved.ProjectId,
            EmployeeId: resolved.EmployeeId);
        var tasks = withoutDates with { Dates = new AnalyticsDateRange(resolved.Basis, resolved.FromUtc, resolved.ToUtcExclusive) };
        return new ScopedTasks(tasks, withoutDates, resolved);
    }

    private async System.Threading.Tasks.Task EnsureInScopeAsync(
        int managerId,
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.EmployeeId is int employeeId && !(await TeamUserIdsAsync(managerId, cancellationToken)).Contains(employeeId))
        {
            throw await _analytics.UserExistsAsync(employeeId, cancellationToken)
                ? new AnalyticsForbiddenException("The employee is outside your team.")
                : new AnalyticsEntityNotFoundException("The employee does not exist.");
        }

        if (filter.ProjectId is int projectId && !(await ProjectIdsAsync(managerId, cancellationToken)).Contains(projectId))
        {
            throw await _analytics.ProjectExistsAsync(projectId, cancellationToken)
                ? new AnalyticsForbiddenException("The project is outside your scope.")
                : new AnalyticsEntityNotFoundException("The project does not exist.");
        }
    }
}
