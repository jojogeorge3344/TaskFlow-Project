using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>An in-app notification targeting a user. Written by INotificationPublisher; the
/// notification center (Screen 12) reads and marks these read in a later change.</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? TaskId { get; set; }
    public Domain.Entities.Task? Task { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}