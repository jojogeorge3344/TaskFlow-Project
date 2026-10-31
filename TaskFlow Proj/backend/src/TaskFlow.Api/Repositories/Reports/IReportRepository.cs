using TaskFlow.Api.Dtos.Reports;
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

/// <summary>What the task report reads. Deleted people's work is kept, with their name and projects.</summary>
public interface IReportRepository : IBaseRepository<TaskFlow.Api.Models.Entities.Task>
{
    /// <summary>Totals of the covered tasks, with "overdue" measured at <paramref name="now"/>.</summary>
    Task<ReportSummaryDto> GetSummaryAsync(ReportCriteria criteria, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The covered tasks by deadline, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<ReportTaskRow>> GetRowsAsync(ReportCriteria criteria, int take, CancellationToken cancellationToken = default);

    /// <summary>Per person, the names of the projects they are allocated to, by name, deleted people included.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetProjectNamesByUserAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);
}
