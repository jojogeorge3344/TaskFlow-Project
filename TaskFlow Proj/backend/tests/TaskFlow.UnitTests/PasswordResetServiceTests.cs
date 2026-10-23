using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

/// <summary>The shared step of both reset flows: an Administrator causes a new password to be generated for a user.
/// The token is null when an Administrator asks from the login page, so no reset link is involved.</summary>
public class PasswordResetServiceTests : IAsyncDisposable
{
    private static readonly DateTime Start = new(2026, 10, 22, 9, 0, 0);
    private const string OldPassword = "OldP@ssw0rd!";

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IPasswordResetService _service;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly FixedTimeProvider _clock = new(Start);
    private readonly User _user;

    public PasswordResetServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);

        _user = new User
        {
            FullName = "Reset Target",
            Username = "reset-target",
            Email = "reset-target@taskflow.com",
            RoleId = SystemRoles.EmployeeId,
            SecurityStamp = "stamp-before-reset",
            IsActive = true,
            CreatedAt = Start,
            UpdatedAt = Start
        };
        _user.PasswordHash = _hasher.HashPassword(_user, OldPassword);

        _db.Users.Add(_user);
        _db.SaveChanges();

        _service = new PasswordResetService(
            _db,
            _hasher,
            new NotificationPublisher(_db, _clock),
            _clock);
    }

    [Fact]
    public async Task ApplyGeneratedPassword_assigns_a_generated_password_the_user_can_sign_in_with()
    {
        var newPassword = await _service.ApplyGeneratedPasswordAsync(_user, token: null, "Password reset.");

        Assert.NotEqual(OldPassword, newPassword);
        Assert.Equal(PasswordGenerator.Length, newPassword.Length);
        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(_user, _user.PasswordHash, newPassword));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(_user, _user.PasswordHash, OldPassword));
    }

    [Fact]
    public async Task ApplyGeneratedPassword_ends_every_existing_session()
    {
        var active = new RefreshToken
        {
            UserId = _user.Id,
            TokenHash = "active-token",
            ExpiresAt = Start.AddDays(7),
            CreatedAt = Start
        };
        var alreadyRevoked = new RefreshToken
        {
            UserId = _user.Id,
            TokenHash = "old-token",
            ExpiresAt = Start.AddDays(7),
            CreatedAt = Start,
            RevokedAt = Start.AddDays(-1)
        };
        _db.RefreshTokens.AddRange(active, alreadyRevoked);
        await _db.SaveChangesAsync();

        await _service.ApplyGeneratedPasswordAsync(_user, token: null, "Password reset.");

        Assert.NotEqual("stamp-before-reset", _user.SecurityStamp);
        Assert.Equal(Start, active.RevokedAt);
        Assert.Equal(Start.AddDays(-1), alreadyRevoked.RevokedAt);
    }

    [Fact]
    public async Task ApplyGeneratedPassword_consumes_the_one_time_link_that_was_used()
    {
        var token = new PasswordResetToken
        {
            UserId = _user.Id,
            TokenHash = "hashed-token",
            ExpiresAt = Start.AddMinutes(5),
            CreatedAt = Start,
            Used = false
        };
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync();

        await _service.ApplyGeneratedPasswordAsync(_user, token, "Password reset by an Administrator.");

        Assert.True(token.Used);
        Assert.Equal(Start, token.ConsumedAt);
    }

    [Fact]
    public async Task ApplyGeneratedPassword_for_the_login_page_flow_needs_no_token_and_still_notifies_the_user()
    {
        // An Administrator triggering Forgot password from the login page has no reset link to consume.
        var newPassword = await _service.ApplyGeneratedPasswordAsync(
            _user,
            token: null,
            "Your password was regenerated after a forgot-password request.");

        Assert.False(string.IsNullOrWhiteSpace(newPassword));

        var notification = await _db.Notifications.SingleAsync(row => row.UserId == _user.Id);
        Assert.Equal(NotificationType.System, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Contains("password was regenerated", notification.Message);
        Assert.Empty(await _db.PasswordResetTokens.ToListAsync());
    }

    [Fact]
    public async Task ApplyGeneratedPassword_records_when_the_account_changed()
    {
        _clock.Set(Start.AddMinutes(2));

        await _service.ApplyGeneratedPasswordAsync(_user, token: null, "Password reset.");

        Assert.Equal(Start.AddMinutes(2), _user.UpdatedAt);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
