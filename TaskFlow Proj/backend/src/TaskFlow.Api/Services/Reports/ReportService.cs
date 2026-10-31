using TaskFlow.Api.Dtos.Reports;
using TaskFlow.Api.Repositories.Reports;
using TaskFlow.Api.Services.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Reports;

/// <summary>Filtered task report aggregation and CSV row projection (Screen 17). Organization-wide
/// for a Manager/Administrator, matching the dashboards' scoping (see the change design, D3), with the
/// shared status and overdue rules (D7). Project is the assignee's project.</summary>
public class ReportService : IReportService
{
    /// <summary>Upper bound on exported rows, so an unfiltered export cannot exhaust memory or
    /// produce an unbounded response.</summary>
    public const int MaxExportRows = 50_000;

    private readonly IReportRepository _reports;
    private readonly IAppClock _clock;

    public ReportService(IReportRepository reports, IAppClock clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public Task<ReportSummaryDto> GetSummaryAsync(ReportFilter filter, CancellationToken cancellationToken) =>
        _reports.GetSummaryAsync(Criteria(filter), _clock.UtcNow, cancellationToken);

    public async Task<IReadOnlyCollection<ReportRowDto>> GetRowsAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await _reports.GetRowsAsync(Criteria(filter), MaxExportRows, cancellationToken);

        // The Project column lists every project the assignee is on. Filters are ignored so a deleted person's work keeps its projects.
        var projectNames = await _reports.GetProjectNamesByUserAsync(
            rows.Select(row => row.AssignedToUserId).Distinct().ToList(),
            cancellationToken);

        return rows
            .Select(row => new ReportRowDto(
                row.TaskCode,
                row.Title,
                row.Assignee,
                projectNames.TryGetValue(row.AssignedToUserId, out var names) ? string.Join(", ", names) : string.Empty,
                row.Priority,
                row.Status,
                row.StartDate,
                row.Deadline,
                row.EstimatedHours,
                row.CompletedAt))
            .ToList();
    }

    private static ReportCriteria Criteria(ReportFilter filter) => new(
        AsUtc(filter.DeadlineFrom),
        AsUtc(filter.DeadlineTo),
        filter.AssignedToUserId,
        filter.ProjectId,
        filter.Status,
        filter.Priority);

    /// <summary>Deadline bounds arrive as query-string values. A `Z`-suffixed instant binds to a
    /// local-kind <see cref="DateTime"/> that preserves the instant but shifts the wall clock, so the
    /// range would drift by the host's UTC offset. Normalise every bound to its UTC instant before
    /// filtering; a value with no offset is treated as UTC, matching how deadlines are stored.</summary>
    private static DateTime? AsUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        _ => value
    };
}
