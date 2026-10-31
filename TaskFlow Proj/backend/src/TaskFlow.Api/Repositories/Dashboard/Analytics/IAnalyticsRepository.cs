using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Models.Entities;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;

namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

/// <summary>A dashboard date range on one of the task dates, end exclusive.</summary>
public record AnalyticsDateRange(AnalyticsDateBasis Basis, DateTime FromUtc, DateTime ToUtcExclusive);

/// <summary>Which counted tasks (live and not cancelled) a dashboard reads, as decided by the scope service. Every given
/// part narrows the set: <see cref="TeamUserIds"/> to tasks assigned to or notifying a team member, <see cref="OwnUserId"/>
/// to tasks assigned to or notifying one person, <see cref="ActiveMembersOfDepartmentId"/> to tasks assigned to an active
/// member of a department, the entity filters as the admin dashboard defines them, and <see cref="Dates"/> to a range.</summary>
public record AnalyticsTaskSet(
    IReadOnlyList<int>? TeamUserIds = null,
    int? OwnUserId = null,
    int? ActiveMembersOfDepartmentId = null,
    int? DepartmentId = null,
    int? ProjectId = null,
    int? EmployeeId = null,
    int? ManagerId = null,
    AnalyticsDateRange? Dates = null)
{
    public AnalyticsTaskSet WithoutDates() => this with { Dates = null };
}

/// <summary>How many instants fell in the quarter hour starting at <see cref="Start"/> (UTC), so a caller can group them
/// into local days in any timezone.</summary>
public record QuarterHourCount(DateTime Start, int Count);

public record OverdueTaskRow(int Id, string TaskCode, string Title, DateTime Deadline, int AssignedToUserId, int? ProjectId);

public record DeadlineTaskRow(int Id, string TaskCode, string Title, TaskPriority Priority, DateTime Deadline);

public record ProjectProgressRow(int ProjectId, int Total, int Completed, DateTime? LatestOpenDeadline);

/// <summary>The queries behind the analytics dashboards. Counts over a task set use one definition of every bucket, so the
/// admin, manager and employee dashboards and the drill-down lists always agree.</summary>
public interface IAnalyticsRepository : IBaseRepository<TaskFlow.Api.Models.Entities.Task>
{
    // ---------------------------------------------------------------- task sets

    Task<IReadOnlyList<int>> GetTaskIdsAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default);

    Task<int> CountTasksAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default);

    Task<BucketCountsDto> CountByStatusAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>One entry per priority, in enum order, including those with no tasks.</summary>
    Task<IReadOnlyList<PriorityCountDto>> CountByPriorityAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default);

    Task<DeadlineBucketsDto> CountByDeadlineAsync(AnalyticsTaskSet set, DeadlineBounds bounds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarterHourCount>> CountCreatedPerQuarterHourAsync(
        AnalyticsTaskSet set,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Completions in the range; with <paramref name="completedStatusOnly"/> only tasks still marked completed.</summary>
    Task<IReadOnlyList<QuarterHourCount>> CountCompletedPerQuarterHourAsync(
        AnalyticsTaskSet set,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        bool completedStatusOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Counts per department of the assignee. A person in several departments counts in each; tasks of people in
    /// none are under key 0.</summary>
    Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByDepartmentAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Counts per person involved: the assignee and every recipient.</summary>
    Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByPersonAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Counts per department manager, a manager's team being everyone in the departments they manage.</summary>
    Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByDepartmentManagerAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Counts per project; tasks without one are under key 0.</summary>
    Task<IReadOnlyDictionary<int, BucketCountsDto>> CountByProjectAsync(AnalyticsTaskSet set, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The latest deadline per project; tasks without one are under key 0.</summary>
    Task<IReadOnlyDictionary<int, DateTime>> GetLatestDeadlineByProjectAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default);

    /// <summary>The overdue tasks with the oldest deadlines first.</summary>
    Task<IReadOnlyList<OverdueTaskRow>> GetOverdueAsync(AnalyticsTaskSet set, DateTime now, int take, CancellationToken cancellationToken = default);

    /// <summary>Tasks not completed with a deadline before <paramref name="beforeUtc"/>, soonest first.</summary>
    Task<IReadOnlyList<DeadlineTaskRow>> GetOpenDueBeforeAsync(AnalyticsTaskSet set, DateTime beforeUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>Per project: total and completed tasks, and the latest deadline of a task not completed.</summary>
    Task<IReadOnlyList<ProjectProgressRow>> GetProjectProgressAsync(AnalyticsTaskSet set, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- people, departments and projects

    /// <summary>Departments by name, optionally only one.</summary>
    Task<IReadOnlyList<AnalyticsOptionDto>> GetDepartmentsAsync(int? only, CancellationToken cancellationToken = default);

    /// <summary>Projects by name, optionally only one and only among <paramref name="within"/>.</summary>
    Task<IReadOnlyList<AnalyticsOptionDto>> GetProjectsAsync(int? only, IReadOnlyCollection<int>? within, CancellationToken cancellationToken = default);

    /// <summary>Every live user by name.</summary>
    Task<IReadOnlyList<AnalyticsOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Live users with the Manager role by name, optionally only one.</summary>
    Task<IReadOnlyList<AnalyticsOptionDto>> GetManagersAsync(int? only, CancellationToken cancellationToken = default);

    /// <summary>Active users whose role does not hold <paramref name="viewAllPermissionKey"/>, narrowed by the filters (the
    /// manager filter meaning the people in the departments that manager manages).</summary>
    Task<int> CountEmployeesAsync(
        string viewAllPermissionKey,
        int? employeeId,
        int? departmentId,
        int? managerId,
        CancellationToken cancellationToken = default);

    Task<int> CountManagersAsync(int? only, CancellationToken cancellationToken = default);

    Task<int> CountDepartmentsAsync(int? only, CancellationToken cancellationToken = default);

    Task<int> CountProjectsAsync(int? only, CancellationToken cancellationToken = default);

    /// <summary>Per department manager, how many people are in the departments they manage.</summary>
    Task<IReadOnlyDictionary<int, int>> GetDepartmentTeamSizesAsync(CancellationToken cancellationToken = default);

    /// <summary>Per manager, how many departments they manage.</summary>
    Task<IReadOnlyDictionary<int, int>> GetManagedDepartmentCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Per manager, how many projects they are assigned to.</summary>
    Task<IReadOnlyDictionary<int, int>> GetManagedProjectCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Users' names by id, deleted accounts included, so history keeps showing them.</summary>
    Task<IReadOnlyDictionary<int, string>> GetUserNamesIncludingDeletedAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Live users' names by id.</summary>
    Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Projects' names by id, deleted projects included.</summary>
    Task<IReadOnlyDictionary<int, string>> GetProjectNamesIncludingDeletedAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task<bool> DepartmentExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ProjectExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The projects a manager is assigned to plus those the team's counted tasks belong to.</summary>
    Task<IReadOnlyList<int>> GetManagerProjectIdsAsync(int managerId, IReadOnlyList<int> teamUserIds, CancellationToken cancellationToken = default);

    /// <summary>The id of the user's department that comes first by name, or null.</summary>
    Task<int?> GetFirstDepartmentIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<string?> GetDepartmentNameAsync(int departmentId, CancellationToken cancellationToken = default);

    Task<int> CountActiveDepartmentMembersAsync(int departmentId, CancellationToken cancellationToken = default);

    /// <summary>The projects the user is allocated to.</summary>
    Task<IReadOnlyList<int>> GetAllocatedProjectIdsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Live projects' id and name among these ids.</summary>
    Task<IReadOnlyList<AnalyticsOptionDto>> GetProjectsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
