using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Reports;

/// <summary>A reported task before its project column is filled in.</summary>
public record ReportTaskRow(
    string TaskCode,
    string Title,
    string Assignee,
    int AssignedToUserId,
    TaskPriority Priority,
    TaskStatus Status,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    DateTime? CompletedAt);
