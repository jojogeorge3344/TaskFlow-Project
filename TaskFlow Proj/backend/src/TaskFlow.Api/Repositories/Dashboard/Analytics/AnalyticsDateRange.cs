using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

/// <summary>A dashboard date range on one of the task dates, end exclusive.</summary>
public record AnalyticsDateRange(AnalyticsDateBasis Basis, DateTime FromUtc, DateTime ToUtcExclusive);
