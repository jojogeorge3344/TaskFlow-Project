using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Communication.Notification.Reminders;

/// <summary>A reminder already recorded in the history.</summary>
public record DeliveredReminder(int TaskId, int RecipientUserId, ReminderType Type, DateTime ScheduledFor);
