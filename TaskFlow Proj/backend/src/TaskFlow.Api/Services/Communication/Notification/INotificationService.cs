using TaskFlow.Api.Dtos.Communication.Notification;
using TaskFlow.Api.Common.Models;

namespace TaskFlow.Api.Services.Communication.Notification;

/// <summary>Reads a user's own in-app notifications (Screen 12). Writes stay owned by
/// <c>INotificationPublisher</c>.</summary>
public interface INotificationService
{
    /// <summary>A page of the caller's notifications, newest first, optionally filtered.</summary>
    Task<PagedResult<NotificationDto>> ListAsync(int userId, NotificationFilter filter, CancellationToken cancellationToken);

    /// <summary>How many of the caller's notifications are still unread.</summary>
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Marks one of the caller's notifications read; returns false when no owned row exists.</summary>
    Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken);

    /// <summary>Marks every one of the caller's notifications read.</summary>
    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken);
}
