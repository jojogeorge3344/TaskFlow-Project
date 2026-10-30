using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Dtos.Communication.Notification;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Communication.Notification;
using NotificationEntity = TaskFlow.Api.Models.Entities.Notification;

namespace TaskFlow.Api.Repositories.Communication.Notification;

public class NotificationRepository : BaseRepository<NotificationEntity>, INotificationRepository
{
    public NotificationRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(
        int userId,
        NotificationType? type,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Where(notification => notification.UserId == userId);

        if (type is { } wantedType)
        {
            query = query.Where(notification => notification.Type == wantedType);
        }

        if (isRead is { } wantedRead)
        {
            query = query.Where(notification => notification.IsRead == wantedRead);
        }

        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .Include(notification => notification.Task)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(notifications.Select(ToDto).ToList(), total, page, pageSize);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

    public Task<NotificationEntity?> GetOwnForUpdateAsync(int userId, int notificationId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<NotificationEntity>> GetUnreadForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        await Set.Where(notification => notification.UserId == userId && !notification.IsRead).ToListAsync(cancellationToken);

    /// <summary>Projects a notification, dropping the task link when the related task is missing or soft-deleted
    /// (design D5).</summary>
    private static NotificationDto ToDto(NotificationEntity notification)
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
