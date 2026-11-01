namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

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
