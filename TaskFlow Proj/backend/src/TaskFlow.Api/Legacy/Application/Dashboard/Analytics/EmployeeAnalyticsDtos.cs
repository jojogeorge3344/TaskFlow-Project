namespace TaskFlow.Application.Dashboard.Analytics;

/// <summary>The period presets of the Employee dashboard trend and completed-work summary.</summary>
public enum EmployeeAnalyticsPeriod
{
    Last7Days,
    Last30Days,
    ThisMonth
}

/// <summary>Aggregate figures of the employee's department. It deliberately has no per-user field, so a colleague's name
/// or task cannot be serialised. <see cref="Tasks"/> is null when the department is too small to show a breakdown
/// (<see cref="Limited"/>), in which case <see cref="TotalTasks"/> is null too, because a total beside the caller's own count would reveal a
/// colleague's workload.</summary>
public record EmployeeDepartmentDto(string Name, bool Limited, int? TotalTasks, BucketCountsDto? Tasks);

public record EmployeeSummaryDto(int TotalTasks, int CompletionPercent, int Completed, int Outstanding);

public record EmployeeDeadlineDto(
    int TaskId,
    string TaskCode,
    string Title,
    string Priority,
    DateTime Deadline,
    int DaysRemaining,
    bool IsOverdue,
    int DaysOverdue);

/// <summary>One project of the employee. All figures are the employee's own tasks in it, never the whole project's.</summary>
public record EmployeeProjectDto(
    int ProjectId,
    string Name,
    int TotalTasks,
    int CompletedTasks,
    int ProgressPercent,
    DateTime? LatestDeadline);

public record EmployeeAnalyticsResponseDto(
    EmployeeAnalyticsPeriod Period,
    DateOnly From,
    DateOnly To,
    EmployeeDepartmentDto? Department,
    EmployeeSummaryDto Summary,
    BucketCountsDto TasksByStatus,
    IReadOnlyList<PriorityCountDto> TasksByPriority,
    IReadOnlyList<EmployeeDeadlineDto> UpcomingDeadlines,
    IReadOnlyList<EmployeeProjectDto> Projects,
    IReadOnlyList<TrendPointDto> Trend,
    int CompletedInPeriod);
