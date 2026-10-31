using TaskFlow.Api.Common.Rules;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

/// <summary>Everything a task list query can filter on. All given filters apply together.</summary>
public record TaskListCriteria(
    TaskVisibility Visibility,
    int? AssignedToUserId = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null,
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    string? Search = null,
    TaskBucketSelection? Bucket = null,
    DeadlineBucket? DeadlineBucket = null,
    DeadlineBounds? DeadlineBounds = null,
    TaskDateRange? DateRange = null,
    int? DepartmentId = null,
    int? ProjectId = null,
    int? EmployeeId = null,
    int? ManagerId = null);
