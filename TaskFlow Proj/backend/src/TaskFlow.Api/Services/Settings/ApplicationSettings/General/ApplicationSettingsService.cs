using Microsoft.Extensions.Options;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Common.Utils;
using TaskFlow.Api.Config;
using TaskFlow.Api.Dtos.Settings.ApplicationSettings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Services.Settings.ApplicationSettings.General;

/// <summary>Maps the application-level rows of the settings table to a typed DTO (Screen 20).</summary>
public class ApplicationSettingsService : IApplicationSettingsService
{
    private static readonly string[] ApplicationKeys =
    [
        SettingKeys.ApplicationName,
        SettingKeys.ApplicationTimezone,
        SettingKeys.ApplicationDefaultPriority,
        SettingKeys.NotificationInAppEnabled
    ];

    private readonly IApplicationSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;

    public ApplicationSettingsService(
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

    public async Task<ApplicationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await _settings.GetValuesAsync(ApplicationKeys, cancellationToken);

        return new ApplicationSettingsDto(
            ReadString(values, SettingKeys.ApplicationName, SettingKeys.DefaultApplicationName),
            ReadString(values, SettingKeys.ApplicationTimezone,
                string.IsNullOrWhiteSpace(_options.Timezone) ? SettingKeys.FallbackTimezone : _options.Timezone),
            ReadPriority(values),
            ReadBool(values, SettingKeys.NotificationInAppEnabled, SettingKeys.DefaultInAppEnabled));
    }

    public async Task<ApplicationSettingsDto> UpdateAsync(
        int userId,
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validator guarantees every field is present; fail loudly rather than store a default.
        if (request.Name is not { } name
            || request.Timezone is not { } timezone
            || request.DefaultPriority is not { } defaultPriority
            || request.InAppNotificationsEnabled is not { } inAppEnabled)
        {
            throw new ArgumentException("Every application setting must be provided.", nameof(request));
        }

        var existing = await _settings.GetForUpdateAsync(ApplicationKeys, cancellationToken);
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

        Set(SettingKeys.ApplicationName, name.Trim(), SettingKeys.ApplicationCategory);
        Set(SettingKeys.ApplicationTimezone, timezone, SettingKeys.ApplicationCategory);
        Set(SettingKeys.ApplicationDefaultPriority, defaultPriority.ToString(), SettingKeys.ApplicationCategory);
        Set(SettingKeys.NotificationInAppEnabled, SettingValues.Format(inAppEnabled), SettingKeys.NotificationCategory);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed) ? parsed : fallback;

    private static TaskPriority ReadPriority(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SettingKeys.ApplicationDefaultPriority, out var raw)
        && Enum.TryParse<TaskPriority>(raw, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : Enum.Parse<TaskPriority>(SettingKeys.DefaultDefaultPriority);
}
