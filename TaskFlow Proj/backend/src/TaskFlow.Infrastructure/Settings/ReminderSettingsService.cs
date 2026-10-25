using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Settings;

/// <summary>Maps the reminder rows of the application settings table to a typed DTO (Screen 13).</summary>
public class ReminderSettingsService : IReminderSettingsService
{
    private static readonly string[] ReminderKeys =
    [
        SettingKeys.ApplicationTimezone,
        SettingKeys.ReminderEnabled,
        SettingKeys.ReminderDeadlineDayEnabled,
        SettingKeys.ReminderDefaultFirstDays,
        SettingKeys.ReminderDefaultSecondDays,
        SettingKeys.ReminderDefaultOverdueFrequency,
        SettingKeys.NotificationMailEnabled
    ];

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;

    public ReminderSettingsService(AppDbContext dbContext, TimeProvider timeProvider, IOptions<ApplicationOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<ReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => ReminderKeys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        return new ReminderSettingsDto(
            ReadBool(values, SettingKeys.ReminderEnabled, SettingKeys.DefaultReminderEnabled),
            ReadBool(values, SettingKeys.ReminderDeadlineDayEnabled, SettingKeys.DefaultDeadlineDayEnabled),
            ReadInt(values, SettingKeys.ReminderDefaultFirstDays, SettingKeys.DefaultFirstDays),
            ReadInt(values, SettingKeys.ReminderDefaultSecondDays, SettingKeys.DefaultSecondDays),
            ReadFrequency(values),
            ReadBool(values, SettingKeys.NotificationMailEnabled, SettingKeys.DefaultMailEnabled),
            values.TryGetValue(SettingKeys.ApplicationTimezone, out var timezone) && !string.IsNullOrWhiteSpace(timezone)
                ? timezone
                : _options.Timezone);
    }

    public async Task<ReminderSettingsDto> UpdateAsync(
        int userId,
        UpdateReminderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validator guarantees every field is present; fail loudly rather than store a default.
        if (request.Enabled is not { } enabled
            || request.DeadlineDayEnabled is not { } deadlineDayEnabled
            || request.DefaultFirstDays is not { } firstDays
            || request.DefaultSecondDays is not { } secondDays
            || request.DefaultOverdueFrequency is not { } overdueFrequency
            || request.MailEnabled is not { } mailEnabled)
        {
            throw new ArgumentException("Every reminder setting must be provided.", nameof(request));
        }

        var existing = await _dbContext.ApplicationSettings
            .Where(setting => ReminderKeys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        void Set(string key, string value, string category)
        {
            if (!existing.TryGetValue(key, out var setting))
            {
                setting = new ApplicationSetting { Key = key, Category = category };
                _dbContext.ApplicationSettings.Add(setting);
            }

            setting.Value = value;
            setting.UpdatedByUserId = userId;
            setting.UpdatedAt = now;
        }

        Set(SettingKeys.ReminderEnabled, SettingsInitializer.Format(enabled), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDeadlineDayEnabled, SettingsInitializer.Format(deadlineDayEnabled), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultFirstDays, SettingsInitializer.Format(firstDays), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultSecondDays, SettingsInitializer.Format(secondDays), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultOverdueFrequency, overdueFrequency.ToString(), SettingKeys.ReminderCategory);
        Set(SettingKeys.NotificationMailEnabled, SettingsInitializer.Format(mailEnabled), SettingKeys.NotificationCategory);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed) ? parsed : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static ReminderFrequency ReadFrequency(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SettingKeys.ReminderDefaultOverdueFrequency, out var raw)
        && Enum.TryParse<ReminderFrequency>(raw, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : Enum.Parse<ReminderFrequency>(SettingKeys.DefaultOverdueFrequency);
}
