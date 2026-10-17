using System.Text.Json;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using User = TaskFlow.Domain.Entities.User;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Settings;

namespace TaskFlow.UnitTests;

public class EmailSettingsServiceTests
{
    private const string Password = "Sup3r-Secret-Pa55";

    private static EmailSettingsService Service(SettingsTestDatabase database, string? password = null, TimeProvider? timeProvider = null) =>
        new(database.Db, timeProvider ?? TimeProvider.System, Options.Create(new EmailOptions { SmtpPassword = password }));

    private static async Task InitializeAsync(SettingsTestDatabase database) =>
        await new SettingsInitializer(database.Db, TimeProvider.System, Options.Create(new ApplicationOptions()))
            .InitializeAsync();

    private static UpdateEmailSettingsRequest Request(bool enabled = true) =>
        new(enabled, " smtp.example.com ", 2525, SmtpSecurity.SslOnConnect, "taskflow@example.com", "TaskFlow Bot", "mailer", "08:30");

    [Fact]
    public async Task Defaults_on_a_fresh_database()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);

        var settings = await Service(database).GetAsync();

        Assert.False(settings.EmailEnabled);
        Assert.Equal("", settings.SmtpHost);
        Assert.Equal(587, settings.SmtpPort);
        Assert.Equal(SmtpSecurity.StartTls, settings.SmtpSecurity);
        Assert.Equal("", settings.SenderAddress);
        Assert.Equal("TaskFlow", settings.SenderName);
        Assert.Equal("", settings.SmtpUsername);
        Assert.Equal("09:00", settings.SendTime);
        Assert.False(settings.PasswordConfigured);
        Assert.Equal([EmailConfigurationCheck.MissingHost, EmailConfigurationCheck.MissingSender], settings.Issues);
    }

    [Fact]
    public async Task Missing_rows_fall_back_to_defaults()
    {
        await using var database = SettingsTestDatabase.Create();

        var settings = await Service(database).GetAsync();

        Assert.Equal(587, settings.SmtpPort);
        Assert.Equal("09:00", settings.SendTime);
    }

    [Fact]
    public async Task Save_round_trips_and_records_who_changed_it()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);
        var admin = new User { FullName = "Asha Admin", Username = "asha", Email = "asha@example.com", Role = Role.Administrator };
        database.Db.Users.Add(admin);
        await database.Db.SaveChangesAsync();
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 17, 6, 0, 0));

        await Service(database, Password, clock).UpdateAsync(admin.Id, Request());
        var settings = await Service(database, Password).GetAsync();

        Assert.True(settings.EmailEnabled);
        Assert.Equal("smtp.example.com", settings.SmtpHost);
        Assert.Equal(2525, settings.SmtpPort);
        Assert.Equal(SmtpSecurity.SslOnConnect, settings.SmtpSecurity);
        Assert.Equal("taskflow@example.com", settings.SenderAddress);
        Assert.Equal("TaskFlow Bot", settings.SenderName);
        Assert.Equal("mailer", settings.SmtpUsername);
        Assert.Equal("08:30", settings.SendTime);
        Assert.True(settings.PasswordConfigured);
        Assert.Empty(settings.Issues);
        Assert.Equal(new DateTime(2026, 10, 17, 6, 0, 0), settings.UpdatedAt);
        Assert.Equal("Asha Admin", settings.UpdatedByName);
    }

    [Fact]
    public async Task Username_without_a_configured_password_is_reported()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);

        var settings = await Service(database, password: null).UpdateAsync(1, Request());

        Assert.False(settings.PasswordConfigured);
        Assert.Equal([EmailConfigurationCheck.MissingPassword], settings.Issues);
    }

    [Fact]
    public async Task Email_switch_is_shared_with_reminder_settings()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);

        await Service(database).UpdateAsync(1, Request(enabled: true));
        Assert.True((await database.ReminderSettings().GetAsync()).EmailEnabled);

        await database.ReminderSettings().UpdateAsync(1, new UpdateReminderSettingsRequest(true, true, 2, 1, ReminderFrequency.Daily, false));
        Assert.False((await Service(database).GetAsync()).EmailEnabled);
        Assert.False((await Service(database).GetScheduleAsync()).Enabled);
    }

    [Fact]
    public async Task Smtp_settings_carry_the_configured_password_for_sending()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);
        await Service(database).UpdateAsync(1, Request());

        var smtp = await Service(database, Password).GetSmtpSettingsAsync();

        Assert.Equal("smtp.example.com", smtp.Host);
        Assert.Equal(Password, smtp.Password);
        Assert.Equal(new TimeOnly(8, 30), (await Service(database).GetScheduleAsync()).SendTime);
    }

    [Fact]
    public async Task Password_never_appears_in_the_settings_response_or_database()
    {
        await using var database = SettingsTestDatabase.Create();
        await InitializeAsync(database);

        var saved = await Service(database, Password).UpdateAsync(1, Request());
        var json = JsonSerializer.Serialize(saved);

        Assert.DoesNotContain(Password, json);
        Assert.DoesNotContain(database.Db.ApplicationSettings, setting => setting.Value.Contains(Password));
    }
}
