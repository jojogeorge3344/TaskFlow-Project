using Microsoft.Extensions.Options;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Config;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Settings.ApplicationSettings.Clock;

/// <summary>Resolves the application timezone from the <c>Application.Timezone</c> setting, falling back to
/// configuration and then to UTC, so a bad value is logged instead of stopping reminders.</summary>
public class AppClock : IAppClock
{
    private readonly IApplicationSettingsRepository _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;
    private readonly ILogger<AppClock> _logger;

    public AppClock(
        IApplicationSettingsRepository settings,
        TimeProvider timeProvider,
        IOptions<ApplicationOptions> options,
        ILogger<AppClock> logger)
    {
        _settings = settings;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetValueAsync(SettingKeys.ApplicationTimezone, cancellationToken);
        var timezoneId = string.IsNullOrWhiteSpace(stored) ? _options.Timezone : stored;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogError(exception, "Unknown application timezone '{TimezoneId}'; using UTC.", timezoneId);
            return TimeZoneInfo.Utc;
        }
    }

    public async Task<DateOnly> TodayLocalAsync(CancellationToken cancellationToken = default)
    {
        var timeZone = await GetTimeZoneAsync(cancellationToken);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, timeZone));
    }
}
