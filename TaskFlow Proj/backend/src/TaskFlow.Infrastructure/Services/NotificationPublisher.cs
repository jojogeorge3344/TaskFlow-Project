using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Writes in-app notifications. The notification center (Screen 12, later change)
/// reads and marks these read.</summary>
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
}