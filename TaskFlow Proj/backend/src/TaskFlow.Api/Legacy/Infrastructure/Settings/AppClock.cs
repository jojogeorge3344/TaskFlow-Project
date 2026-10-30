using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Settings;

/// <summary>Resolves the application timezone from the <c>Application.Timezone</c> setting, falling back to
/// configuration and then to UTC, so a bad value is logged instead of stopping reminders.</summary>
public class AppClock : IAppClock
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;
    private readonly ILogger<AppClock> _logger;

    public AppClock(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<ApplicationOptions> options,
        ILogger<AppClock> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SettingKeys.ApplicationTimezone)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

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
