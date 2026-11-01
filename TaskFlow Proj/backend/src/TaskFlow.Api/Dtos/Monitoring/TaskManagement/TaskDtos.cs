using ReminderFrequency = TaskFlow.Api.Models.Enums.ReminderFrequency;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement;

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
    bool IsOverdue,
    int? ProjectId = null,
    string? ProjectName = null);

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
    decimal? ActualHours,
    TaskStatus Status,
    string Stage,
    IReadOnlyCollection<TaskRecipientDto> Recipients,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? ProjectId = null,
    string? ProjectName = null,
    int FirstReminderDays = 2,
    int SecondReminderDays = 1,
    ReminderFrequency OverdueReminderFrequency = ReminderFrequency.Daily);

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
    bool IsOverdue,
    int? ProjectId = null,
    string? ProjectName = null);
