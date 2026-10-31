using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Communication.Notification.Reminders;

/// <summary>An open task that may need a deadline reminder.</summary>
public record ReminderCandidate(int Id, string Title, int CreatedByUserId, ReminderTaskSnapshot Snapshot);
