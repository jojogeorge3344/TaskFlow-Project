using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Dtos.Communication.Notification;
using TaskFlow.Api.Models.Enums;
using NotificationEntity = TaskFlow.Api.Models.Entities.Notification;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Communication.Notification.Notifications;

/// <summary>Users' in-app notifications.</summary>
public interface INotificationRepository : IBaseRepository<NotificationEntity>
{
    /// <summary>One page of the user's notifications, newest first. A notification's task link is dropped when the task
    /// is missing or deleted.</summary>
    Task<PagedResult<NotificationDto>> ListAsync(
        int userId,
        NotificationType? type,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The user's own tracked notification, for marking it read.</summary>
    Task<NotificationEntity?> GetOwnForUpdateAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    /// <summary>The user's tracked unread notifications, for marking them read.</summary>
    Task<IReadOnlyList<NotificationEntity>> GetUnreadForUpdateAsync(int userId, CancellationToken cancellationToken = default);
}
