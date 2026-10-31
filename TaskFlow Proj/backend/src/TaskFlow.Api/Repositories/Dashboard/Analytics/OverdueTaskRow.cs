namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

public record OverdueTaskRow(int Id, string TaskCode, string Title, DateTime Deadline, int AssignedToUserId, int? ProjectId);
