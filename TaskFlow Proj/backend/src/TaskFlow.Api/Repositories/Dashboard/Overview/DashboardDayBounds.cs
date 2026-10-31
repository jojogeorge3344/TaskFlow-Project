namespace TaskFlow.Api.Repositories.Dashboard.Overview;

/// <summary>The local day bounds a dashboard counts against, as UTC instants.</summary>
public record DashboardDayBounds(DateTime Now, DateTime TodayStart, DateTime TomorrowStart, DateTime UpcomingHorizon);
