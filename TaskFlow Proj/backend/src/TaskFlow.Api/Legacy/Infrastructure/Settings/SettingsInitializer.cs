using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Settings;

/// <summary>Inserts every required application setting that is missing, with its default value. Rows that
/// already exist are never overwritten, so values changed in the UI survive restarts. These rows are
/// required configuration, not demo data, so this runs whether or not seeding is enabled.</summary>
public class SettingsInitializer
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;

    public SettingsInitializer(AppDbContext dbContext, TimeProvider timeProvider, IOptions<ApplicationOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var existingKeys = await _dbContext.ApplicationSettings
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var missing = Defaults()
            .Where(setting => !existingKeys.Contains(setting.Key))
            .Select(setting => new ApplicationSetting
            {
                Key = setting.Key,
                Value = setting.Value,
                Category = setting.Category,
                UpdatedAt = now
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        _dbContext.ApplicationSettings.AddRange(missing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IEnumerable<(string Key, string Value, string Category)> Defaults()
    {
        var timezone = string.IsNullOrWhiteSpace(_options.Timezone) ? SettingKeys.FallbackTimezone : _options.Timezone;

        yield return (SettingKeys.ApplicationTimezone, timezone, SettingKeys.ApplicationCategory);
        yield return (SettingKeys.ApplicationName, SettingKeys.DefaultApplicationName, SettingKeys.ApplicationCategory);
        yield return (SettingKeys.ApplicationDefaultPriority, SettingKeys.DefaultDefaultPriority, SettingKeys.ApplicationCategory);
        yield return (SettingKeys.ReminderEnabled, Format(SettingKeys.DefaultReminderEnabled), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDeadlineDayEnabled, Format(SettingKeys.DefaultDeadlineDayEnabled), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultFirstDays, Format(SettingKeys.DefaultFirstDays), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultSecondDays, Format(SettingKeys.DefaultSecondDays), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultOverdueFrequency, SettingKeys.DefaultOverdueFrequency, SettingKeys.ReminderCategory);
        yield return (SettingKeys.NotificationMailEnabled, Format(SettingKeys.DefaultMailEnabled), SettingKeys.NotificationCategory);
        yield return (SettingKeys.NotificationInAppEnabled, Format(SettingKeys.DefaultInAppEnabled), SettingKeys.NotificationCategory);
    }

    internal static string Format(bool value) => value ? "true" : "false";

    internal static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
