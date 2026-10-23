using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class PasswordResetTokenServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IPasswordResetTokenService _service;
    private readonly int _userId;

    public PasswordResetTokenServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);

        var user = new User
        {
            FullName = "Test User",
            Username = "testuser",
            Email = "test@taskflow.com",
            RoleId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Password123!");
        _db.Users.Add(user);
        _db.SaveChanges();

        _userId = user.Id;

        _service = new PasswordResetTokenService(_db, TimeProvider.System);
    }

    [Fact]
    public async Task RequestResetToken_stores_hash_not_plaintext()
    {
        var token = await _service.RequestResetTokenAsync(_userId);

        var stored = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.UserId == _userId);

        Assert.NotNull(stored);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow);
        Assert.True(stored.ExpiresAt <= DateTime.UtcNow.AddMinutes(5));
        Assert.False(stored.Used);
    }

    [Fact]
    public async Task ValidateToken_accepts_valid_token()
    {
        var token = await _service.RequestResetTokenAsync(_userId);

        var user = await _service.ValidateTokenAsync(token);

        Assert.NotNull(user);
        Assert.Equal(_userId, user.Id);
    }

    [Fact]
    public async Task ValidateToken_rejects_unknown_token()
    {
        var user = await _service.ValidateTokenAsync("nonexistent-token");

        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateToken_rejects_used_token()
    {
        var token = await _service.RequestResetTokenAsync(_userId);

        // First use succeeds.
        var user1 = await _service.ValidateTokenAsync(token);
        Assert.NotNull(user1);

        // Consuming the token marks it used, as a successful reset does.
        var stored = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == _userId);
        await _service.MarkUsedAsync(stored!, DateTime.UtcNow);

        // Second use fails.
        var user2 = await _service.ValidateTokenAsync(token);
        Assert.Null(user2);
    }

    [Fact]
    public async Task CreateAsync_inserts_token_that_IsReadableByItsHash()
    {
        var hash = ComputeHash("precomputed-token");

        var created = await _service.CreateAsync(_userId, hash, DateTime.UtcNow.AddMinutes(5));

        var found = await _service.FindByTokenHashAsync(hash);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal(_userId, found.UserId);
        Assert.False(found.Used);
        Assert.NotNull(found.User);
    }

    [Fact]
    public async Task ValidateToken_rejects_expired_token()
    {
        // Create a token with an expired expiry, then generate its hash so ValidateTokenAsync finds it.
        var plaintext = "expired-but-valid-hash";
        var hash = ComputeHash(plaintext);
        var expired = new PasswordResetToken
        {
            UserId = _userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            Used = false
        };
        _db.PasswordResetTokens.Add(expired);
        await _db.SaveChangesAsync();

        var user = await _service.ValidateTokenAsync(plaintext);
        Assert.Null(user);
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public async Task MarkUsedAsync_records_consumed_time()
    {
        var token = await _service.RequestResetTokenAsync(_userId);
        var stored = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == _userId);
        Assert.NotNull(stored);
        Assert.False(stored.Used);

        await _service.MarkUsedAsync(stored, DateTime.UtcNow);

        var again = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Id == stored.Id);
        Assert.True(again!.Used);
        Assert.NotNull(again.ConsumedAt);
    }

    [Fact]
    public async Task Notification_publisher_creates_notification_on_password_reset()
    {
        // Arrange: a valid token and a notification publisher.
        var token = await _service.RequestResetTokenAsync(_userId);
        var publisher = new NotificationPublisher(_db, TimeProvider.System);

        // Act: publish a password-reset notification.
        await publisher.PublishAsync(
            _userId,
            null,
            TaskFlow.Domain.Enums.NotificationType.System,
            "Your password has been reset by an Administrator. Please sign in with your new password.");

        // Assert: notification row exists for the user.
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _userId && n.Type == TaskFlow.Domain.Enums.NotificationType.System);

        Assert.NotNull(notification);
        Assert.False(notification.IsRead);
        Assert.Contains("password has been reset", notification.Message);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
