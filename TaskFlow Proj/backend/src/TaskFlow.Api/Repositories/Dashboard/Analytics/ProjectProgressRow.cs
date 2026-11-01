namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

public record ProjectProgressRow(int ProjectId, int Total, int Completed, DateTime? LatestOpenDeadline);
