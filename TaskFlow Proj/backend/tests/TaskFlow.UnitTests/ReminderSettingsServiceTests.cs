using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Dtos.Settings.ReminderSettings;
using TaskFlow.Api.Config;
using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests;

public class ReminderSettingsServiceTests
{
    private static async Task Initialize(SettingsTestDatabase database) =>
        await new SettingsInitializer(database.Db, TimeProvider.System, Options.Create(new ApplicationOptions { Timezone = "Asia/Kolkata" }))
            .InitializeAsync();

    [Fact]
    public async Task Fresh_database_returns_the_defaults()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);

        var settings = await database.ReminderSettings().GetAsync();

        Assert.Equal(new ReminderSettingsDto(true, true, 2, 1, ReminderFrequency.Daily, true, "Asia/Kolkata"), settings);
    }

    [Fact]
    public async Task Missing_rows_fall_back_to_defaults_and_configured_timezone()
    {
        await using var database = SettingsTestDatabase.Create();

        var settings = await database.ReminderSettings(configuredTimezone: "Asia/Kolkata").GetAsync();

        Assert.Equal(new ReminderSettingsDto(true, true, 2, 1, ReminderFrequency.Daily, true, "Asia/Kolkata"), settings);
    }

    [Fact]
    public async Task Unreadable_values_fall_back_to_defaults()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ReminderDefaultFirstDays, "lots");
        database.SetValue(SettingKeys.ReminderEnabled, "maybe");
        database.SetValue(SettingKeys.ReminderDefaultOverdueFrequency, "Hourly");

        var settings = await database.ReminderSettings().GetAsync();

        Assert.Equal(2, settings.DefaultFirstDays);
        Assert.True(settings.Enabled);
        Assert.Equal(ReminderFrequency.Daily, settings.DefaultOverdueFrequency);
    }

    [Fact]
    public async Task Saved_values_round_trip()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);
        var request = new UpdateReminderSettingsRequest(false, false, 3, 0, ReminderFrequency.Weekly, true);

        var saved = await database.ReminderSettings().UpdateAsync(userId: 7, request);
        var reloaded = await database.ReminderSettings().GetAsync();

        Assert.Equal(new ReminderSettingsDto(false, false, 3, 0, ReminderFrequency.Weekly, true, "Asia/Kolkata"), saved);
        Assert.Equal(saved, reloaded);
    }

    [Fact]
    public async Task Save_records_who_and_when_and_leaves_timezone_unchanged()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 17, 9, 0, 0));

        await database.ReminderSettings(clock).UpdateAsync(7, new UpdateReminderSettingsRequest(true, true, 3, 1, ReminderFrequency.Daily, false));

        var first = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ReminderDefaultFirstDays);
        Assert.Equal("3", first.Value);
        Assert.Equal(7, first.UpdatedByUserId);
        Assert.Equal(new DateTime(2026, 10, 17, 9, 0, 0), first.UpdatedAt);

        var timezone = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationTimezone);
        Assert.Equal("Asia/Kolkata", timezone.Value);
        Assert.Null(timezone.UpdatedByUserId);
    }
}
