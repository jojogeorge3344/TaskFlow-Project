using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Settings.Validators;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Settings;

/// <summary>Maps the email rows of the application settings table to a typed DTO (Screen 14). The email switch
/// is the shared <c>Notification.EmailEnabled</c> key that Reminder Settings also edits.</summary>
public class EmailSettingsService : IEmailSettingsService
{
    private static readonly string[] EmailKeys =
    [
        SettingKeys.NotificationEmailEnabled,
        SettingKeys.EmailSmtpHost,
        SettingKeys.EmailSmtpPort,
        SettingKeys.EmailSmtpSecurity,
        SettingKeys.EmailSenderAddress,
        SettingKeys.EmailSenderName,
        SettingKeys.EmailSmtpUsername,
        SettingKeys.EmailSendTime
    ];

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _options;

    public EmailSettingsService(AppDbContext dbContext, TimeProvider timeProvider, IOptions<EmailOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadRowsAsync(cancellationToken);
        var values = rows.ToDictionary(row => row.Key, row => row.Value);
        var host = ReadString(values, SettingKeys.EmailSmtpHost, SettingKeys.DefaultSmtpHost);
        var sender = ReadString(values, SettingKeys.EmailSenderAddress, SettingKeys.DefaultSenderAddress);
        var username = ReadString(values, SettingKeys.EmailSmtpUsername, SettingKeys.DefaultSmtpUsername);

        var latest = rows.OrderByDescending(row => row.UpdatedAt).FirstOrDefault();
        string? updatedByName = null;
        if (latest?.UpdatedByUserId is { } updatedBy)
        {
            updatedByName = await _dbContext.Users
                .Where(user => user.Id == updatedBy)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new EmailSettingsDto(
            ReadBool(values, SettingKeys.NotificationEmailEnabled, SettingKeys.DefaultEmailEnabled),
            host,
            ReadPort(values),
            ReadSecurity(values),
            sender,
            ReadString(values, SettingKeys.EmailSenderName, SettingKeys.DefaultSenderName),
            username,
            ReadSendTime(values).ToString("HH:mm", CultureInfo.InvariantCulture),
            _options.PasswordConfigured,
            EmailConfigurationCheck.Problems(host, sender, username, _options.PasswordConfigured),
            latest?.UpdatedAt,
            updatedByName);
    }

    public async Task<EmailSettingsDto> UpdateAsync(
        int userId,
        UpdateEmailSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validator guarantees these are present; fail loudly rather than store a default.
        if (request.EmailEnabled is not { } enabled
            || request.SmtpPort is not { } port
            || request.SmtpSecurity is not { } security
            || request.SendTime is null)
        {
            throw new ArgumentException("Every email setting must be provided.", nameof(request));
        }

        var existing = await _dbContext.ApplicationSettings
            .Where(setting => EmailKeys.Contains(setting.Key))
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

        Set(SettingKeys.NotificationEmailEnabled, SettingsInitializer.Format(enabled), SettingKeys.NotificationCategory);
        Set(SettingKeys.EmailSmtpHost, Clean(request.SmtpHost), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSmtpPort, SettingsInitializer.Format(port), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSmtpSecurity, security.ToString(), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSenderAddress, Clean(request.SenderAddress), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSenderName, Clean(request.SenderName), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSmtpUsername, Clean(request.SmtpUsername), SettingKeys.EmailCategory);
        Set(SettingKeys.EmailSendTime, request.SendTime.Trim(), SettingKeys.EmailCategory);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    public async Task<SmtpSettings> GetSmtpSettingsAsync(CancellationToken cancellationToken = default)
    {
        var values = (await LoadRowsAsync(cancellationToken)).ToDictionary(row => row.Key, row => row.Value);

        return new SmtpSettings(
            ReadString(values, SettingKeys.EmailSmtpHost, SettingKeys.DefaultSmtpHost),
            ReadPort(values),
            ReadSecurity(values),
            ReadString(values, SettingKeys.EmailSenderAddress, SettingKeys.DefaultSenderAddress),
            ReadString(values, SettingKeys.EmailSenderName, SettingKeys.DefaultSenderName),
            ReadString(values, SettingKeys.EmailSmtpUsername, SettingKeys.DefaultSmtpUsername),
            _options.SmtpPassword);
    }

    public async Task<EmailSchedule> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        var values = (await LoadRowsAsync(cancellationToken)).ToDictionary(row => row.Key, row => row.Value);

        return new EmailSchedule(
            ReadBool(values, SettingKeys.NotificationEmailEnabled, SettingKeys.DefaultEmailEnabled),
            ReadSendTime(values));
    }

    private Task<List<ApplicationSetting>> LoadRowsAsync(CancellationToken cancellationToken) =>
        _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => EmailKeys.Contains(setting.Key))
            .ToListAsync(cancellationToken);

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var raw) ? raw : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed) ? parsed : fallback;

    private static int ReadPort(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SettingKeys.EmailSmtpPort, out var raw)
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        && parsed is >= 1 and <= 65535
            ? parsed
            : SettingKeys.DefaultSmtpPort;

    private static SmtpSecurity ReadSecurity(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SettingKeys.EmailSmtpSecurity, out var raw)
        && Enum.TryParse<SmtpSecurity>(raw, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : Enum.Parse<SmtpSecurity>(SettingKeys.DefaultSmtpSecurity);

    private static TimeOnly ReadSendTime(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SettingKeys.EmailSendTime, out var raw) && UpdateEmailSettingsRequestValidator.BeAValidSendTime(raw)
            ? TimeOnly.ParseExact(raw, "HH:mm", CultureInfo.InvariantCulture)
            : TimeOnly.ParseExact(SettingKeys.DefaultSendTime, "HH:mm", CultureInfo.InvariantCulture);
}
