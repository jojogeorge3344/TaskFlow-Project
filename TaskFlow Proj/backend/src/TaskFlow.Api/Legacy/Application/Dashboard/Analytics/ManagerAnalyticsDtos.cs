namespace TaskFlow.Application.Dashboard.Analytics;

public record TeamSummaryDto(int TeamSize, int TotalProjects, BucketCountsDto Tasks)
{
    public int TotalTasks => Tasks.Total;

    public int CompletionPercent => Tasks.CompletionPercent;
}

/// <summary>One team member's tasks. <see cref="HighWorkload"/> and <see cref="HasOverdue"/> are computed on the server
/// from the thresholds in <c>ManagerTeamThresholds</c>.</summary>
public record TeamEmployeeDto(int UserId, string Name, BucketCountsDto Tasks, bool HighWorkload, bool HasOverdue);

public record DelayedTaskDto(
    int TaskId,
    string TaskCode,
    string Title,
    string AssigneeName,
    string? ProjectName,
    int DaysOverdue);

public record ProjectAttentionDto(int ProjectId, string Name, int OverdueTasks);

public record TeamAttentionDto(
    IReadOnlyList<DelayedTaskDto> DelayedTasks,
    IReadOnlyList<ProjectAttentionDto> ProjectsNeedingAttention);

public record ManagerAnalyticsResponseDto(
    AppliedFilterDto Filter,
    TeamSummaryDto Summary,
    BucketCountsDto TasksByStatus,
    IReadOnlyList<PriorityCountDto> TasksByPriority,
    IReadOnlyList<TeamEmployeeDto> ByEmployee,
    IReadOnlyList<ProjectAnalyticsDto> ByProject,
    TeamAttentionDto Attention);

/// <summary>Filter choices for a manager: only in-scope employees and projects, and no department list.</summary>
public record ManagerFilterOptionsDto(
    DateOnly Today,
    string Timezone,
    IReadOnlyList<AnalyticsOptionDto> Employees,
    IReadOnlyList<AnalyticsOptionDto> Projects);
