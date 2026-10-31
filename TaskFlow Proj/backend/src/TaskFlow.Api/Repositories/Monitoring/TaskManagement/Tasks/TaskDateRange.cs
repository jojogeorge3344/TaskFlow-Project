using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

/// <summary>A dashboard date range on one of the task dates, end exclusive.</summary>
public record TaskDateRange(AnalyticsDateBasis Basis, DateTime FromUtc, DateTime ToUtcExclusive);
