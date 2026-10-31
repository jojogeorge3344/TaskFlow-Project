using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Communication.Notification.Notifications;

namespace TaskFlow.Api.Services.Communication.Notification.Publishing;

/// <summary>Append-only write seam for in-app notifications. The notification center (Screen 12)
/// reads and marks these read through INotificationService.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(int userId, int? taskId, NotificationType type, string message, CancellationToken cancellationToken = default);
}