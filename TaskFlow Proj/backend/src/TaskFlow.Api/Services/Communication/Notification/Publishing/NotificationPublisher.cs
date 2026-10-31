using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using NotificationEntity = TaskFlow.Api.Models.Entities.Notification;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Communication.Notification.Notifications;
using TaskFlow.Api.Services.Communication.Notification.Notifications;

namespace TaskFlow.Api.Services.Communication.Notification.Publishing;

/// <summary>Writes in-app notifications. The notification center (Screen 12) reads and marks these
/// read through INotificationService. New rows are skipped while the global in-app preference
/// (Screen 20) is off; existing notifications are never removed.</summary>
public class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRepository _notifications;
    private readonly IApplicationSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public NotificationPublisher(
        INotificationRepository notifications,
        IApplicationSettingsRepository settings,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _notifications = notifications;
        _settings = settings;
        _unitOfWork = unitOfWork;
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

        _notifications.Add(new NotificationEntity
        {
            UserId = userId,
            TaskId = taskId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Defaults to enabled when the row is missing or unreadable, so a half-seeded store
    /// never silently mutes notifications.</summary>
    private async Task<bool> InAppEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await _settings.GetValueAsync(SettingKeys.NotificationInAppEnabled, cancellationToken);
        return raw is null || !bool.TryParse(raw, out var enabled) || enabled;
    }
}
