using TaskFlow.Api.Common.Rules;

namespace TaskFlow.Api.Dtos.Dashboard.Analytics;

public record BucketCountsDto(int Pending, int InProgress, int Completed, int Overdue)
{
    public int Total => Pending + InProgress + Completed + Overdue;

    public int Open => Pending + InProgress + Overdue;

    public int CompletionPercent => TaskBucketRules.CompletionPercent(Completed, Total);
}

public record AppliedFilterDto(
    DateOnly From,
    DateOnly To,
    AnalyticsDateBasis Basis,
    int? DepartmentId,
    int? ProjectId,
    int? EmployeeId,
    int? ManagerId);

public record AnalyticsSummaryDto(
    int TotalEmployees,
    int TotalManagers,
    int TotalDepartments,
    int TotalProjects,
    BucketCountsDto Tasks)
{
    public int TotalTasks => Tasks.Total;

    public int CompletionPercent => Tasks.CompletionPercent;
}

public record PriorityCountDto(string Priority, int Count);

public record DeadlineBucketsDto(int Overdue, int DueToday, int Next7Days, int Later);

public record TrendPointDto(DateOnly Date, int Created, int Completed);

public record TrendDto(string Granularity, IReadOnlyList<TrendPointDto> Points);

public record DepartmentAnalyticsDto(int? DepartmentId, string Name, BucketCountsDto Tasks);

public record EmployeeAnalyticsDto(int UserId, string Name, BucketCountsDto Tasks);

public record EmployeeAnalyticsSectionDto(IReadOnlyList<EmployeeAnalyticsDto> Items, int TotalEmployees);

public record ManagerAnalyticsDto(
    int ManagerId,
    string Name,
    int Departments,
    int Employees,
    int Projects,
    BucketCountsDto Tasks);

public record ProjectAnalyticsDto(
    int? ProjectId,
    string Name,
    BucketCountsDto Tasks,
    DateTime? ProjectedEnd)
{
    public int Outstanding => Tasks.Open;
}

public record AdminAnalyticsDto(
    AppliedFilterDto Filter,
    AnalyticsSummaryDto Summary,
    BucketCountsDto TasksByStatus,
    IReadOnlyList<PriorityCountDto> TasksByPriority,
    DeadlineBucketsDto DeadlineBuckets,
    TrendDto Trend,
    IReadOnlyList<DepartmentAnalyticsDto> ByDepartment,
    EmployeeAnalyticsSectionDto ByEmployee,
    IReadOnlyList<ManagerAnalyticsDto> ByManager,
    IReadOnlyList<ProjectAnalyticsDto> ByProject);

public record AnalyticsOptionDto(int Id, string Name);

public record AnalyticsFilterOptionsDto(
    DateOnly Today,
    string Timezone,
    IReadOnlyList<AnalyticsOptionDto> Departments,
    IReadOnlyList<AnalyticsOptionDto> Projects,
    IReadOnlyList<AnalyticsOptionDto> Employees,
    IReadOnlyList<AnalyticsOptionDto> Managers);
