using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Reports;

/// <summary>Which live tasks a report covers. The deadline bounds are UTC instants, both inclusive; the project means a
/// project the assignee is allocated to.</summary>
public record ReportCriteria(
    DateTime? DeadlineFromUtc,
    DateTime? DeadlineToUtc,
    int? AssignedToUserId,
    int? ProjectId,
    TaskStatus? Status,
    TaskPriority? Priority);
