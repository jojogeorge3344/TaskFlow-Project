using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>An in-app notification targeting a user. Written by INotificationPublisher and read
/// and marked read by INotificationService in the notification center (Screen 12).</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? TaskId { get; set; }
    public TaskFlow.Api.Models.Entities.Task? Task { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}