using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Services.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Monitoring.EmployeeWorkload;

/// <summary>Per-employee workload aggregation (Screen 16). Covers every active employee
/// (every user whose role does not hold <c>tasks.view.all</c>, which is the Employee role by default), organization-wide for a Manager/Administrator, matching the
/// dashboards' scoping (see the change design, D3).</summary>
public class WorkloadService : IWorkloadService
{
    private readonly IWorkloadRepository _workload;
    private readonly IAppClock _clock;

    public WorkloadService(IWorkloadRepository workload, IAppClock clock)
    {
        _workload = workload;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<EmployeeWorkloadDto>> GetWorkloadAsync(
        int? projectId,
        CancellationToken cancellationToken)
    {
        var users = await _workload.GetEmployeesAsync(PermissionKeys.TasksViewAll, projectId, cancellationToken);
        if (users.Count == 0)
        {
            return Array.Empty<EmployeeWorkloadDto>();
        }

        var counts = await _workload.GetTaskCountsAsync(users.Select(user => user.Id).ToList(), _clock.UtcNow, cancellationToken);
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
}
