namespace TaskFlow.Api.Services.Interfaces.Communication.Notification.Reminders;

/// <summary>Generates the deadline reminders that are due right now (BRD §3). Safe to call any number of
/// times: each (task, recipient, reminder type, day) is delivered at most once.</summary>
public interface IReminderService
{
    /// <summary>Generates every reminder due at the current time and returns how many notifications were written.</summary>
    Task<int> GenerateDueAsync(CancellationToken cancellationToken = default);
}
