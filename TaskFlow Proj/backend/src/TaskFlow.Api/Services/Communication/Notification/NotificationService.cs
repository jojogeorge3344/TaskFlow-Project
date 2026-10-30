using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Dtos.Communication.Notification;
using TaskFlow.Api.Repositories.Interfaces;
using TaskFlow.Api.Repositories.Interfaces.Communication.Notification;
using TaskFlow.Api.Services.Interfaces.Communication.Notification;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Api.Services.Communication.Notification;

/// <summary>Reads a user's own in-app notifications for the Notification Center (Screen 12).
/// Writes stay owned by <see cref="NotificationPublisher"/>.</summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<NotificationDto>> ListAsync(
        int userId,
        NotificationFilter filter,
        CancellationToken cancellationToken)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        return _notifications.ListAsync(userId, filter.Type, filter.IsRead, page, pageSize, cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken) =>
        _notifications.CountUnreadAsync(userId, cancellationToken);

    public async Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetOwnForUpdateAsync(userId, notificationId, cancellationToken);
        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken)
    {
        var unread = await _notifications.GetUnreadForUpdateAsync(userId, cancellationToken);
        if (unread.Count == 0)
        {
            return;
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
