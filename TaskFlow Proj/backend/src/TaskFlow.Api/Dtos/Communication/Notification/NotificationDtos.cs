using TaskFlow.Application.Tasks;
using NotificationType = TaskFlow.Api.Models.Enums.NotificationType;

namespace TaskFlow.Api.Dtos.Communication.Notification;

/// <summary>A row in the Notification Center (Screen 12): the notification plus the task it links
/// to, when that task still exists.</summary>
public record NotificationDto(
    int Id,
    string Type,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    int? TaskId,
    string? TaskCode);

/// <summary>Filters and paging for the notification list (Screen 12).</summary>
public record NotificationFilter(
    NotificationType? Type = null,
    bool? IsRead = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>The caller's unread notification count, for the header badge (Screen 12).</summary>
public record UnreadCountDto(int Count);
