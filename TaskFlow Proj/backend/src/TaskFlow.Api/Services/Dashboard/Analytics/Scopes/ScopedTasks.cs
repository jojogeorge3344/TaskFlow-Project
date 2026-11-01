using TaskFlow.Api.Repositories.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Scopes;

/// <summary>The tasks a dashboard reads, decided by its scope: with the date range, and the same set without it (for the
/// trend, which spans its own range).</summary>
public sealed record ScopedTasks(
    AnalyticsTaskSet Tasks,
    AnalyticsTaskSet TasksIgnoringDates,
    ResolvedAnalyticsFilter Filter);
