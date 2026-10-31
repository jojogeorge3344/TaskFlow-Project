namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

/// <summary>How many instants fell in the quarter hour starting at <see cref="Start"/> (UTC), so a caller can group them
/// into local days in any timezone.</summary>
public record QuarterHourCount(DateTime Start, int Count);
