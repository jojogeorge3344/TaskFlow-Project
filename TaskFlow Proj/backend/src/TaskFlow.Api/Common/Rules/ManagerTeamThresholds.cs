namespace TaskFlow.Domain.Rules;

/// <summary>The thresholds behind the Manager dashboard's per-employee flags, kept in one place so they can be tuned.</summary>
public static class ManagerTeamThresholds
{
    /// <summary>A member is "High workload" when their open tasks are at least this multiple of the team mean...</summary>
    public const double HighWorkloadFactor = 1.5;

    /// <summary>...and at least this many.</summary>
    public const int HighWorkloadMinimumOpen = 5;

    /// <summary>How many of the most overdue tasks and projects the attention list shows.</summary>
    public const int AttentionListSize = 5;

    public static bool IsHighWorkload(int openTasks, double teamMeanOpen) =>
        openTasks >= HighWorkloadMinimumOpen && openTasks >= HighWorkloadFactor * teamMeanOpen;

    public static bool HasOverdue(int overdueTasks) => overdueTasks > 0;
}
