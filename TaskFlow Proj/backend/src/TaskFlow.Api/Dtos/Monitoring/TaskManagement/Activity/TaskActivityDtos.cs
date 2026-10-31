namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;

public record ActivityEntryDto(
    int Id,
    string EventType,
    int ActorUserId,
    string ActorName,
    string? OldValue,
    string? NewValue,
    string Message,
    DateTime CreatedAt);

public record UpdateHoursEntryRequest(decimal Hours);
