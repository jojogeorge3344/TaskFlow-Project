using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Infrastructure.Settings;

namespace TaskFlow.UnitTests;

public class SettingsInitializerTests
{
    private static SettingsInitializer Initializer(SettingsTestDatabase database, string timezone = "Asia/Kolkata") =>
        new(database.Db, TimeProvider.System, Options.Create(new ApplicationOptions { Timezone = timezone }));

    [Fact]
    public async Task Inserts_every_default_key_once()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database).InitializeAsync();
        await Initializer(database).InitializeAsync();

        var keys = await database.Db.ApplicationSettings.Select(setting => setting.Key).ToListAsync();
        Assert.Equal(14, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(SettingKeys.ReminderEnabled, keys);
        Assert.Contains(SettingKeys.NotificationEmailEnabled, keys);
    }

    [Fact]
    public async Task Timezone_is_seeded_from_configuration()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database, "Asia/Kolkata").InitializeAsync();

        var timezone = await database.Db.ApplicationSettings.SingleAsync(setting => setting.Key == SettingKeys.ApplicationTimezone);
        Assert.Equal("Asia/Kolkata", timezone.Value);
    }

    [Fact]
    public async Task Existing_values_are_never_overwritten()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initializer(database).InitializeAsync();
        database.SetValue(SettingKeys.ReminderDefaultFirstDays, "3");

        await Initializer(database).InitializeAsync();

        var first = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ReminderDefaultFirstDays);
        Assert.Equal("3", first.Value);
    }

    [Fact]
    public async Task Only_missing_keys_are_added()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ReminderEnabled, "false");

        await Initializer(database).InitializeAsync();

        var enabled = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ReminderEnabled);
        Assert.Equal("false", enabled.Value);
        Assert.Equal(14, await database.Db.ApplicationSettings.CountAsync());
    }

    [Fact]
    public async Task Email_defaults_are_seeded_without_a_password()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database).InitializeAsync();

        var values = await database.Db.ApplicationSettings
            .Where(setting => setting.Category == SettingKeys.EmailCategory)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value);
        Assert.Equal(7, values.Count);
        Assert.Equal("", values[SettingKeys.EmailSmtpHost]);
        Assert.Equal("587", values[SettingKeys.EmailSmtpPort]);
        Assert.Equal("StartTls", values[SettingKeys.EmailSmtpSecurity]);
        Assert.Equal("TaskFlow", values[SettingKeys.EmailSenderName]);
        Assert.Equal("09:00", values[SettingKeys.EmailSendTime]);
        Assert.DoesNotContain(values.Keys, key => key.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly EmailOptions Gmail = new()
    {
        SmtpHost = "smtp.gmail.com",
        SmtpPort = 587,
        SmtpSecurity = "StartTls",
        SenderAddress = "jojogeorge3344@gmail.com",
        SenderName = "jojosmtp",
        SmtpUsername = "jojogeorge3344@gmail.com",
        SendTime = "08:30"
    };

    private static SettingsInitializer Initializer(SettingsTestDatabase database, EmailOptions email) =>
        new(database.Db, TimeProvider.System, Options.Create(new ApplicationOptions()), Options.Create(email));

    private static Task<Dictionary<string, string>> EmailValuesAsync(SettingsTestDatabase database) =>
        database.Db.ApplicationSettings
            .AsNoTracking()
            .Where(setting => setting.Category == SettingKeys.EmailCategory)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value);

    [Fact]
    public async Task Smtp_fields_are_seeded_from_configuration()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database, Gmail).InitializeAsync();

        var values = await EmailValuesAsync(database);
        Assert.Equal("smtp.gmail.com", values[SettingKeys.EmailSmtpHost]);
        Assert.Equal("587", values[SettingKeys.EmailSmtpPort]);
        Assert.Equal("StartTls", values[SettingKeys.EmailSmtpSecurity]);
        Assert.Equal("jojogeorge3344@gmail.com", values[SettingKeys.EmailSenderAddress]);
        Assert.Equal("jojosmtp", values[SettingKeys.EmailSenderName]);
        Assert.Equal("jojogeorge3344@gmail.com", values[SettingKeys.EmailSmtpUsername]);
        Assert.Equal("08:30", values[SettingKeys.EmailSendTime]);
    }

    [Fact]
    public async Task Invalid_configured_values_fall_back_to_defaults()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database, new EmailOptions { SmtpPort = 70000, SmtpSecurity = "Carrier pigeon", SendTime = "25:00" }).InitializeAsync();

        var values = await EmailValuesAsync(database);
        Assert.Equal("587", values[SettingKeys.EmailSmtpPort]);
        Assert.Equal("StartTls", values[SettingKeys.EmailSmtpSecurity]);
        Assert.Equal("09:00", values[SettingKeys.EmailSendTime]);
    }

    [Fact]
    public async Task Configuration_never_overwrites_values_saved_on_the_screen()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initializer(database, Gmail).InitializeAsync();
        database.SetValue(SettingKeys.EmailSmtpHost, "smtp.office365.com");

        await Initializer(database, Gmail).InitializeAsync();

        Assert.Equal("smtp.office365.com", (await EmailValuesAsync(database))[SettingKeys.EmailSmtpHost]);
    }
}
