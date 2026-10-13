using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tasks;

public record TaskRecipientDto(int UserId, string FullName);

public record TaskCardDto(
    int Id,
    string TaskCode,
    string Title,
    int AssignedToUserId,
    string AssignedToName,
    TaskPriority Priority,
    DateTime Deadline,
    TaskStatus Status,
    bool IsOverdue);

public record TaskDetailsDto(
    int Id,
    string TaskCode,
    string Title,
    string? Description,
    int AssignedToUserId,
    string AssignedToName,
    int CreatedByUserId,
    string CreatedByName,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    TaskStatus Status,
    string Stage,
    IReadOnlyCollection<TaskRecipientDto> Recipients,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>A row on the paginated Task List (Screen 7): card fields plus creator, start date
/// and estimated hours, with the deadline state derived at read time.</summary>
public record TaskListItemDto(
    int Id,
    string TaskCode,
    string Title,
    int AssignedToUserId,
    string AssignedToName,
    int CreatedByUserId,
    string CreatedByName,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    TaskStatus Status,
    string Stage,
    bool IsOverdue);

/// <summary>One page of results with the total count and page metadata (TDD §8.4).</summary>
public record PagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);

public record TaskCommentDto(int Id, string Body, int AuthorUserId, string AuthorName, DateTime CreatedAt);

public record ActivityEntryDto(
    int Id,
    string EventType,
    int ActorUserId,
    string ActorName,
    string? OldValue,
    string? NewValue,
    string Message,
    DateTime CreatedAt);
