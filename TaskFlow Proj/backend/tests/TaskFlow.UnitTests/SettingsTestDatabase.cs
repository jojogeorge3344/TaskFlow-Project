using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Settings;

namespace TaskFlow.UnitTests;

/// <summary>An in-memory SQLite database for settings and clock tests.</summary>
internal sealed class SettingsTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SettingsTestDatabase(SqliteConnection connection, AppDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public AppDbContext Db { get; }

    public static SettingsTestDatabase Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        TestAccessControl.Seed(db);

        return new SettingsTestDatabase(connection, db);
    }

    public void SetValue(string key, string value)
    {
        var existing = Db.ApplicationSettings.SingleOrDefault(setting => setting.Key == key);
        if (existing is null)
        {
            Db.ApplicationSettings.Add(new ApplicationSetting
            {
                Key = key,
                Value = value,
                Category = "Test",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
        }

        Db.SaveChanges();
    }

    public AppClock Clock(TimeProvider? timeProvider = null, string configuredTimezone = "UTC") =>
        new(
            Db,
            timeProvider ?? TimeProvider.System,
            Options.Create(new ApplicationOptions { Timezone = configuredTimezone }),
            NullLogger<AppClock>.Instance);

    public ReminderSettingsService ReminderSettings(TimeProvider? timeProvider = null, string configuredTimezone = "UTC") =>
        new(
            Db,
            timeProvider ?? TimeProvider.System,
            Options.Create(new ApplicationOptions { Timezone = configuredTimezone }));

    public ApplicationSettingsService ApplicationSettings(TimeProvider? timeProvider = null, string configuredTimezone = "UTC") =>
        new(
            Db,
            timeProvider ?? TimeProvider.System,
            Options.Create(new ApplicationOptions { Timezone = configuredTimezone }));

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>A clock frozen at a given UTC instant.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow)
    {
        _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    public void Set(DateTime utcNow) => _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;
}
