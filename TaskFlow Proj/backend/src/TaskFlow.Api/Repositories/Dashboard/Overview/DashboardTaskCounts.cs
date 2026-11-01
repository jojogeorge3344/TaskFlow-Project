namespace TaskFlow.Api.Repositories.Dashboard.Overview;

/// <summary>Counts of the scoped live tasks. Open means neither completed nor cancelled; due today, upcoming (tomorrow up
/// to the horizon) and overdue count open tasks only.</summary>
public record DashboardTaskCounts(int Total, int Open, int DueToday, int Upcoming, int Overdue, int InProgress, int Completed);
