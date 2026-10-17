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
        Assert.Equal(7, keys.Count);
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
        Assert.Equal(7, await database.Db.ApplicationSettings.CountAsync());
    }
}
