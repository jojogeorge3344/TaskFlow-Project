using TaskFlow.Api.Dtos.Settings.ApplicationSettings;
using TaskFlow.Api.Common.Constants;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests;

public class ApplicationSettingsServiceTests
{
    private static async Task Initialize(SettingsTestDatabase database) =>
        await new TaskFlow.Api.Persistence.Seeders.SettingsInitializer(
            database.Db, TimeProvider.System, Microsoft.Extensions.Options.Options.Create(
                new TaskFlow.Api.Config.ApplicationOptions { Timezone = "Asia/Kolkata" }))
            .InitializeAsync();

    [Fact]
    public async Task Fresh_database_returns_the_defaults()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);

        var settings = await database.ApplicationSettings().GetAsync();

        Assert.Equal(new ApplicationSettingsDto("TaskFlow", "Asia/Kolkata", TaskPriority.Medium, true), settings);
    }

    [Fact]
    public async Task Missing_rows_fall_back_to_defaults_and_configured_timezone()
    {
        await using var database = SettingsTestDatabase.Create();

        var settings = await database.ApplicationSettings(configuredTimezone: "Asia/Kolkata").GetAsync();

        Assert.Equal(new ApplicationSettingsDto("TaskFlow", "Asia/Kolkata", TaskPriority.Medium, true), settings);
    }

    [Fact]
    public async Task Unreadable_values_fall_back_to_defaults()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ApplicationDefaultPriority, "Urgent");
        database.SetValue(SettingKeys.NotificationInAppEnabled, "perhaps");

        var settings = await database.ApplicationSettings().GetAsync();

        Assert.Equal(TaskPriority.Medium, settings.DefaultPriority);
        Assert.True(settings.InAppNotificationsEnabled);
    }

    [Fact]
    public async Task Saved_values_round_trip()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);
        var request = new UpdateApplicationSettingsRequest("Acme Tasks", "UTC", TaskPriority.Critical, false);

        var saved = await database.ApplicationSettings().UpdateAsync(userId: 4, request);
        var reloaded = await database.ApplicationSettings().GetAsync();

        Assert.Equal(new ApplicationSettingsDto("Acme Tasks", "UTC", TaskPriority.Critical, false), saved);
        Assert.Equal(saved, reloaded);
    }

    [Fact]
    public async Task Save_records_who_and_when()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 18, 8, 30, 0));

        await database.ApplicationSettings(clock).UpdateAsync(
            4, new UpdateApplicationSettingsRequest("Acme", "UTC", TaskPriority.High, true));

        var name = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationName);
        Assert.Equal("Acme", name.Value);
        Assert.Equal(4, name.UpdatedByUserId);
        Assert.Equal(new DateTime(2026, 10, 18, 8, 30, 0), name.UpdatedAt);
    }

    [Fact]
    public async Task Incomplete_request_fails_loudly_and_saves_nothing()
    {
        await using var database = SettingsTestDatabase.Create();
        await Initialize(database);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.ApplicationSettings().UpdateAsync(4, new UpdateApplicationSettingsRequest(null, "UTC", null, true)));

        var name = await database.Db.ApplicationSettings.AsNoTracking().SingleAsync(setting => setting.Key == SettingKeys.ApplicationName);
        Assert.Equal("TaskFlow", name.Value);
    }
}
