using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Communication.Notification.Reminders;

/// <summary>A task's notification recipient.</summary>
public record TaskRecipientLink(int TaskId, int UserId);
