using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Settings;

/// <summary>Inserts every required application setting that is missing, with its default value. Rows that
/// already exist are never overwritten, so values changed in the UI survive restarts. These rows are
/// required configuration, not demo data, so this runs whether or not seeding is enabled. The SMTP fields
/// take their first values from the <c>Email</c> configuration section when it provides them.</summary>
public class SettingsInitializer
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationOptions _options;
    private readonly EmailOptions _emailOptions;

    public SettingsInitializer(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<ApplicationOptions> options,
        IOptions<EmailOptions>? emailOptions = null)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _emailOptions = emailOptions?.Value ?? new EmailOptions();
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
        yield return (SettingKeys.ReminderEnabled, Format(SettingKeys.DefaultReminderEnabled), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDeadlineDayEnabled, Format(SettingKeys.DefaultDeadlineDayEnabled), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultFirstDays, Format(SettingKeys.DefaultFirstDays), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultSecondDays, Format(SettingKeys.DefaultSecondDays), SettingKeys.ReminderCategory);
        yield return (SettingKeys.ReminderDefaultOverdueFrequency, SettingKeys.DefaultOverdueFrequency, SettingKeys.ReminderCategory);
        yield return (SettingKeys.NotificationEmailEnabled, Format(SettingKeys.DefaultEmailEnabled), SettingKeys.NotificationCategory);
        var email = _emailOptions;
        var port = email.SmtpPort is >= 1 and <= 65535 ? email.SmtpPort.Value : SettingKeys.DefaultSmtpPort;
        var security = Enum.TryParse<Domain.Enums.SmtpSecurity>(email.SmtpSecurity, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed.ToString()
            : SettingKeys.DefaultSmtpSecurity;
        var sendTime = Application.Settings.Validators.UpdateEmailSettingsRequestValidator.BeAValidSendTime(email.SendTime)
            ? email.SendTime!
            : SettingKeys.DefaultSendTime;

        yield return (SettingKeys.EmailSmtpHost, OrDefault(email.SmtpHost, SettingKeys.DefaultSmtpHost), SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSmtpPort, Format(port), SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSmtpSecurity, security, SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSenderAddress, OrDefault(email.SenderAddress, SettingKeys.DefaultSenderAddress), SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSenderName, OrDefault(email.SenderName, SettingKeys.DefaultSenderName), SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSmtpUsername, OrDefault(email.SmtpUsername, SettingKeys.DefaultSmtpUsername), SettingKeys.EmailCategory);
        yield return (SettingKeys.EmailSendTime, sendTime, SettingKeys.EmailCategory);
    }

    private static string OrDefault(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

    internal static string Format(bool value) => value ? "true" : "false";

    internal static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
