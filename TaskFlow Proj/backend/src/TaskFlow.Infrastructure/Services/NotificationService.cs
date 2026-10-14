using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Notifications;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Reads a user's own in-app notifications for the Notification Center (Screen 12).
/// Writes stay owned by <see cref="NotificationPublisher"/>.</summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;

    public NotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(
        int userId,
        NotificationFilter filter,
        CancellationToken cancellationToken)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (filter.Type is NotificationType type)
        {
            query = query.Where(notification => notification.Type == type);
        }

        if (filter.IsRead is bool isRead)
        {
            query = query.Where(notification => notification.IsRead == isRead);
        }

        var total = await query.CountAsync(cancellationToken);

        var notifications = await query
            .Include(notification => notification.Task)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = notifications.Select(ToDto).ToList();
        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken)
    {
        return _dbContext.Notifications.CountAsync(
            notification => notification.UserId == userId && !notification.IsRead,
            cancellationToken);
    }

    public async Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken)
    {
        var notification = await _dbContext.Notifications.FirstOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == userId,
            cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken)
    {
        var unread = await _dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Projects a notification, dropping the task link when the related task is
    /// missing or soft-deleted (design D5).</summary>
    private static NotificationDto ToDto(Notification notification)
    {
        var task = notification.Task is { IsDeleted: false } linkedTask ? linkedTask : null;

        return new NotificationDto(
            notification.Id,
            notification.Type.ToString(),
            notification.Message,
            notification.IsRead,
            notification.CreatedAt,
            task?.Id,
            task?.TaskCode);
    }
}
