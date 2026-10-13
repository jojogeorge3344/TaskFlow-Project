using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

/// <summary>Append-only write seam for in-app notifications. The notification center (Screen 12,
/// later change) reads and marks these read.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(int userId, int? taskId, NotificationType type, string message, CancellationToken cancellationToken = default);
}