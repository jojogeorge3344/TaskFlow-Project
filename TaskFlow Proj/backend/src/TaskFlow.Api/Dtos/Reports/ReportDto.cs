using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Dtos.Reports;

/// <summary>Filters for a task report (Screen 17). Every field is optional; an omitted filter leaves
/// that dimension unrestricted. The date range filters the task deadline inclusively.</summary>
public record ReportFilter(
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    int? AssignedToUserId = null,
    int? ProjectId = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null);

/// <summary>Totals for the filtered scope, used for the headline figures and the completion summary.</summary>
public record ReportSummaryDto(
    int TotalTasks,
    int PendingTasks,
    int CompletedTasks,
    int OverdueTasks);

/// <summary>One exported task row. Project is the assignee's project, matching the report filter.</summary>
public record ReportRowDto(
    string TaskCode,
    string Title,
    string Assignee,
    string Project,
    TaskPriority Priority,
    TaskStatus Status,
    DateTime StartDate,
    DateTime Deadline,
    decimal EstimatedHours,
    DateTime? CompletedAt);
