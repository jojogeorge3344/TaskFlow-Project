using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Interfaces.Communication.Notification;

/// <summary>Append-only write seam for in-app notifications. The notification center (Screen 12)
/// reads and marks these read through INotificationService.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(int userId, int? taskId, NotificationType type, string message, CancellationToken cancellationToken = default);
}