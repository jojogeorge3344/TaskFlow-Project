using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tasks;

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