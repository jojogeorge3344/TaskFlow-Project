using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Tasks;

public record ActivityFilter(
    TaskActivityEventType? EventType = null,
    int? ActorUserId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);