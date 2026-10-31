using TaskFlow.Api.Common.Constants;

namespace TaskFlow.UnitTests.Settings.ApplicationSettings;

public class AppClockTests
{
    [Fact]
    public async Task Stored_iana_timezone_is_used()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ApplicationTimezone, "Asia/Kolkata");

        var timeZone = await database.Clock().GetTimeZoneAsync();

        Assert.Equal(TimeSpan.FromHours(5.5), timeZone.BaseUtcOffset);
    }

    [Fact]
    public async Task Configured_timezone_is_used_while_no_setting_is_stored()
    {
        await using var database = SettingsTestDatabase.Create();

        var timeZone = await database.Clock(configuredTimezone: "Asia/Kolkata").GetTimeZoneAsync();

        Assert.Equal(TimeSpan.FromHours(5.5), timeZone.BaseUtcOffset);
    }

    [Fact]
    public async Task Unknown_timezone_falls_back_to_utc()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ApplicationTimezone, "Not/AZone");

        var timeZone = await database.Clock().GetTimeZoneAsync();

        Assert.Equal(TimeZoneInfo.Utc, timeZone);
    }

    [Fact]
    public async Task Today_is_the_local_date_in_the_application_timezone()
    {
        await using var database = SettingsTestDatabase.Create();
        database.SetValue(SettingKeys.ApplicationTimezone, "Asia/Kolkata");

        // 19:00 UTC on 14 October is 00:30 IST on 15 October.
        var clock = database.Clock(new FixedTimeProvider(new DateTime(2026, 10, 14, 19, 0, 0)));

        Assert.Equal(new DateOnly(2026, 10, 15), await clock.TodayLocalAsync());
    }
}
