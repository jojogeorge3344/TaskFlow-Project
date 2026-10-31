namespace TaskFlow.Api.Services.Settings.ApplicationSettings;

/// <summary>The single application timezone (BR-17) and the current time. Timestamps stay UTC; this
/// only answers "which calendar day is it" for deadline comparisons.</summary>
public interface IAppClock
{
    DateTime UtcNow { get; }

    /// <summary>The configured application timezone, or UTC when the configured id is unknown.</summary>
    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default);

    /// <summary>Today's date in the application timezone.</summary>
    Task<DateOnly> TodayLocalAsync(CancellationToken cancellationToken = default);
}
