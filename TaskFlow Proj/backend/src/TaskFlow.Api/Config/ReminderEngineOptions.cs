namespace TaskFlow.Api.Config;

/// <summary>The <c>Reminders</c> configuration section.</summary>
public class ReminderEngineOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Turns the background scheduler on or off (tests and rollbacks switch it off).</summary>
    public bool SchedulerEnabled { get; set; } = true;

    /// <summary>Seconds between reminder passes.</summary>
    public int TickSeconds { get; set; } = 60;
}
