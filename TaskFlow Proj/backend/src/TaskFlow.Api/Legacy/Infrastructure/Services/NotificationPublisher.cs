using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Writes in-app notifications. The notification center (Screen 12) reads and marks these
/// read through INotificationService. New rows are skipped while the global in-app preference
/// (Screen 20) is off; existing notifications are never removed.</summary>
public class NotificationPublisher : INotificationPublisher
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public NotificationPublisher(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task PublishAsync(
        int userId,
        int? taskId,
        NotificationType type,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!await InAppEnabledAsync(cancellationToken))
        {
            return;
        }

        _dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            TaskId = taskId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Defaults to enabled when the row is missing or unreadable, so a half-seeded store
    /// never silently mutes notifications.</summary>
    private async Task<bool> InAppEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SettingKeys.NotificationInAppEnabled)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return raw is null || !bool.TryParse(raw, out var enabled) || enabled;
    }
}
