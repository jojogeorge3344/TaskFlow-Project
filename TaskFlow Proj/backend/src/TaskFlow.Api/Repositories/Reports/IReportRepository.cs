using TaskFlow.Api.Dtos.Reports;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Reports;

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
