using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Config;
using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        Assert.Equal(10, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(SettingKeys.ReminderEnabled, keys);
        Assert.Contains(SettingKeys.NotificationMailEnabled, keys);
        Assert.Contains(SettingKeys.ApplicationName, keys);
        Assert.Contains(SettingKeys.ApplicationDefaultPriority, keys);
        Assert.Contains(SettingKeys.NotificationInAppEnabled, keys);
    }

    [Fact]
    public async Task New_application_keys_are_seeded_with_defaults()
    {
        await using var database = SettingsTestDatabase.Create();

        await Initializer(database).InitializeAsync();

        var name = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationName);
        var priority = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationDefaultPriority);
        var inApp = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.NotificationInAppEnabled);
        Assert.Equal("TaskFlow", name.Value);
        Assert.Equal("Medium", priority.Value);
        Assert.Equal("true", inApp.Value);
    }

    [Fact]
    public async Task Saved_application_name_is_not_overwritten()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initializer(database).InitializeAsync();
        database.SetValue(SettingKeys.ApplicationName, "Acme Tasks");

        await Initializer(database).InitializeAsync();

        var name = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationName);
        Assert.Equal("Acme Tasks", name.Value);
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
        Assert.Equal(10, await database.Db.ApplicationSettings.CountAsync());
    }
}
