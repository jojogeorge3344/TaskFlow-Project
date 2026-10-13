using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using ReminderFrequency = TaskFlow.Domain.Enums.ReminderFrequency;

namespace TaskFlow.Application.Tasks;

public record CreateTaskRequest(
    string Title,
    string? Description,
    int AssignedToUserId,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    IReadOnlyCollection<int>? AdditionalRecipientUserIds = null,
    int FirstReminderDays = 2,
    int SecondReminderDays = 1,
    ReminderFrequency OverdueReminderFrequency = ReminderFrequency.Daily);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours);

public record UpdateTaskStatusRequest(TaskStatus Status);

public record ReassignTaskRequest(int NewAssigneeUserId, string Reason);

public record AddCommentRequest(string Body);

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
    int PageSize = 20);