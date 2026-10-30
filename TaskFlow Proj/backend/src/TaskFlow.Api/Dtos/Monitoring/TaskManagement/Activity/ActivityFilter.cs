using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;

public record ActivityFilter(
    TaskActivityEventType? EventType = null,
    int? ActorUserId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);