using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Api.Common.Rules;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using ReminderFrequency = TaskFlow.Api.Models.Enums.ReminderFrequency;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement;

/// <summary>A new task. Omitted (null) reminder fields take the configured reminder defaults (Screen 13).
/// A project is mandatory: an omitted ProjectId (0) is rejected.</summary>
public record CreateTaskRequest(
    string Title,
    string? Description,
    int AssignedToUserId,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    IReadOnlyCollection<int>? AdditionalRecipientUserIds = null,
    int? FirstReminderDays = null,
    int? SecondReminderDays = null,
    ReminderFrequency? OverdueReminderFrequency = null,
    int ProjectId = 0);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    decimal? ActualHours = null);

public record UpdateTaskStatusRequest(TaskStatus Status);

public record ReassignTaskRequest(int NewAssigneeUserId, string Reason);

/// <summary>Adds worked time to a task's running total.</summary>
public record LogHoursRequest(decimal Hours);

public record SetRecipientsRequest(IReadOnlyCollection<int> RecipientUserIds);

/// <summary>Filters and paging for the paginated task list (TDD §8.4).</summary>
public record TaskListFilter(
    int? AssignedToUserId = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null,
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    TaskBucketFilter? Bucket = null,
    DeadlineBucket? DeadlineBucket = null,
    int? DepartmentId = null,
    int? ProjectId = null,
    int? EmployeeId = null,
    int? ManagerId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    AnalyticsDateBasis Basis = AnalyticsDateBasis.Created);

public enum TaskBucketFilter
{
    All,
    Pending,
    InProgress,
    Completed,
    Overdue
}