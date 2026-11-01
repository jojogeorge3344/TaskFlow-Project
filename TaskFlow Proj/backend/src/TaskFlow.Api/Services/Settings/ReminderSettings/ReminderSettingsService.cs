using System.Globalization;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Utils;
using TaskFlow.Api.Config;
using TaskFlow.Api.Dtos.Settings.ReminderSettings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Services.Settings.ReminderSettings;

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

    private readonly IApplicationSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;

    public ReminderSettingsService(
        IApplicationSettingsRepository settings,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<ApplicationOptions> options)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<ReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await _settings.GetValuesAsync(ReminderKeys, cancellationToken);

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

        var existing = await _settings.GetForUpdateAsync(ReminderKeys, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        void Set(string key, string value, string category)
        {
            if (!existing.TryGetValue(key, out var setting))
            {
                setting = new ApplicationSetting { Key = key, Category = category };
                _settings.Add(setting);
            }

            setting.Value = value;
            setting.UpdatedByUserId = userId;
            setting.UpdatedAt = now;
        }

        Set(SettingKeys.ReminderEnabled, SettingValues.Format(enabled), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDeadlineDayEnabled, SettingValues.Format(deadlineDayEnabled), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultFirstDays, SettingValues.Format(firstDays), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultSecondDays, SettingValues.Format(secondDays), SettingKeys.ReminderCategory);
        Set(SettingKeys.ReminderDefaultOverdueFrequency, overdueFrequency.ToString(), SettingKeys.ReminderCategory);
        Set(SettingKeys.NotificationMailEnabled, SettingValues.Format(mailEnabled), SettingKeys.NotificationCategory);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
