using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Projects;
using TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Services.Settings.ApplicationSettings.Clock;

namespace TaskFlow.Api.Services.Monitoring.TaskManagement.AssigneeCandidates;

/// <summary>Ranks a project's team for a new task: fewest active tasks first, then fewest overdue, then the best on-time
/// completion rate (no history last), then name and id so the pick is stable. Allocation percentage plays no part. The
/// team and the counts follow the Employee Workload screen's rules.</summary>
public class AssigneeCandidateService : IAssigneeCandidateService
{
    private readonly IWorkloadRepository _workload;
    private readonly IProjectRepository _projects;
    private readonly IAppClock _clock;

    public AssigneeCandidateService(IWorkloadRepository workload, IProjectRepository projects, IAppClock clock)
    {
        _workload = workload;
        _projects = projects;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AssigneeCandidateDto>?> GetCandidatesAsync(int projectId, CancellationToken cancellationToken)
    {
        if (!await _projects.ExistsActiveAsync(projectId, cancellationToken))
        {
            return null;
        }

        var team = await _workload.GetEmployeesAsync(PermissionKeys.TasksViewAll, projectId, cancellationToken);
        if (team.Count == 0)
        {
            return Array.Empty<AssigneeCandidateDto>();
        }

        var counts = await _workload.GetTaskCountsAsync(team.Select(user => user.Id).ToList(), _clock.UtcNow, cancellationToken);
        var byUser = counts.ToDictionary(entry => entry.UserId);

        return team
            .Select(user =>
            {
                byUser.TryGetValue(user.Id, out var count);
                double? onTimeRate = count is { Completed: > 0 } ? (double)count.CompletedOnTime / count.Completed : null;
                return new AssigneeCandidateDto(
                    user.Id,
                    user.FullName,
                    user.EmployeeCode,
                    count?.Active ?? 0,
                    count?.Overdue ?? 0,
                    onTimeRate);
            })
            .OrderBy(candidate => candidate.ActiveTasks)
            .ThenBy(candidate => candidate.OverdueTasks)
            .ThenBy(candidate => candidate.OnTimeRate is null)
            .ThenByDescending(candidate => candidate.OnTimeRate)
            .ThenBy(candidate => candidate.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.UserId)
            .ToList();
    }
}
