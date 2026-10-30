using TaskFlow.Api.Dtos.Reports;

namespace TaskFlow.Api.Services.Interfaces.Reports;

public interface IReportService
{
    /// <summary>Totals and completion summary for the filtered scope (Screen 17).</summary>
    Task<ReportSummaryDto> GetSummaryAsync(ReportFilter filter, CancellationToken cancellationToken);

    /// <summary>The task rows behind the same filter, for the CSV export.</summary>
    Task<IReadOnlyCollection<ReportRowDto>> GetRowsAsync(ReportFilter filter, CancellationToken cancellationToken);
}
