using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

/// <summary>The single-use reset token behind "forgot password" on the login page, and the password change it allows.</summary>
public class PasswordRecoveryServiceTests : IAsyncDisposable
{
    private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0);
    private const string OldPassword = "OldP@ssw0rd!";
    private const string NewPassword = "NewP@ssw0rd!";

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly FixedTimeProvider _clock = new(Start);
    private readonly PasswordRecoveryService _service;
    private readonly User _user;

    public PasswordRecoveryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);

        _user = AddUser("recover-user");
        _service = new PasswordRecoveryService(_db, _hasher, new NotificationPublisher(_db, _clock), Microsoft.Extensions.Logging.Abstractions.NullLogger<PasswordRecoveryService>.Instance, _clock);
    }

    private User AddUser(string username)
    {
        var user = new User
        {
            FullName = username,
            Username = username,
            Email = $"{username}@taskflow.com",
            RoleId = SystemRoles.EmployeeId,
            SecurityStamp = "stamp-before",
            IsActive = true,
            CreatedAt = Start,
            UpdatedAt = Start
        };
        user.PasswordHash = _hasher.HashPassword(user, OldPassword);
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task A_token_is_issued_valid_for_ten_minutes_and_stored_only_as_a_hash()
    {
        var token = await _service.IssueResetTokenAsync(_user.Id);

        var grant = await _db.PasswordResetGrants.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotEqual(token, grant.TokenHash);
        Assert.DoesNotContain(token, grant.TokenHash);
        Assert.Equal(Start.AddMinutes(10), grant.ExpiresAt);
        Assert.Null(grant.UsedAt);
    }

    [Fact]
    public async Task Two_tokens_are_never_the_same()
    {
        var first = await _service.IssueResetTokenAsync(_user.Id);
        var second = await _service.IssueResetTokenAsync(_user.Id);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Reset_sets_the_new_password_ends_sessions_and_burns_the_token()
    {
        var session = new RefreshToken { UserId = _user.Id, TokenHash = "active", ExpiresAt = Start.AddDays(7), CreatedAt = Start };
        _db.RefreshTokens.Add(session);
        await _db.SaveChangesAsync();
        var token = await _service.IssueResetTokenAsync(_user.Id);

        var reset = await _service.ResetPasswordAsync(token, NewPassword);

        Assert.True(reset);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, _user.PasswordHash, NewPassword));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(_user, _user.PasswordHash, OldPassword));
        Assert.NotEqual("stamp-before", _user.SecurityStamp);
        Assert.Equal(Start, session.RevokedAt);
        Assert.False(await _service.ResetPasswordAsync(token, "AnotherP@ss1"));
    }

    [Fact]
    public async Task Reset_fails_for_an_unknown_or_expired_token_and_changes_nothing()
    {
        var token = await _service.IssueResetTokenAsync(_user.Id);

        Assert.False(await _service.ResetPasswordAsync("not-a-real-token", NewPassword));

        _clock.Set(Start.AddMinutes(11));
        Assert.False(await _service.ResetPasswordAsync(token, NewPassword));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, _user.PasswordHash, OldPassword));
    }

    [Fact]
    public async Task A_reset_also_spends_every_other_outstanding_token_for_that_account()
    {
        var first = await _service.IssueResetTokenAsync(_user.Id);
        var second = await _service.IssueResetTokenAsync(_user.Id);

        Assert.True(await _service.ResetPasswordAsync(first, NewPassword));

        Assert.False(await _service.ResetPasswordAsync(second, "AnotherP@ss1"));
    }

    [Fact]
    public async Task A_token_cannot_reset_a_different_account()
    {
        var other = AddUser("recover-other");
        var token = await _service.IssueResetTokenAsync(_user.Id);

        await _service.ResetPasswordAsync(token, NewPassword);

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == other.Id);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(other, stored.PasswordHash, OldPassword));
    }

    [Fact]
    public async Task A_deleted_account_cannot_be_reset()
    {
        var token = await _service.IssueResetTokenAsync(_user.Id);
        _user.IsDeleted = true;
        await _db.SaveChangesAsync();

        Assert.False(await _service.ResetPasswordAsync(token, NewPassword));
    }


    [Fact]
    public async Task A_deactivated_account_cannot_be_reset_even_with_a_live_token()
    {
        var token = await _service.IssueResetTokenAsync(_user.Id);
        _user.IsActive = false;
        await _db.SaveChangesAsync();

        Assert.False(await _service.ResetPasswordAsync(token, NewPassword));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, _user.PasswordHash, OldPassword));
    }

    [Fact]
    public async Task A_successful_reset_tells_the_owner_in_app()
    {
        var token = await _service.IssueResetTokenAsync(_user.Id);

        await _service.ResetPasswordAsync(token, NewPassword);

        Assert.Contains(await _db.Notifications.ToListAsync(), n => n.UserId == _user.Id && n.Message.Contains("reset with your authenticator"));
    }

    [Fact]
    public async Task A_failing_notification_does_not_undo_or_fail_the_reset()
    {
        var service = new PasswordRecoveryService(
            _db, _hasher, new ThrowingPublisher(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PasswordRecoveryService>.Instance, _clock);
        var token = await service.IssueResetTokenAsync(_user.Id);

        Assert.True(await service.ResetPasswordAsync(token, NewPassword));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, _user.PasswordHash, NewPassword));
    }

    private sealed class ThrowingPublisher : TaskFlow.Application.Tasks.INotificationPublisher
    {
        public Task PublishAsync(int userId, int? taskId, TaskFlow.Domain.Enums.NotificationType type, string message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Notifications are down.");
    }

    [Fact]
    public async Task A_failed_reset_sends_no_notification()
    {
        await _service.ResetPasswordAsync("not-a-token", NewPassword);

        Assert.Empty(await _db.Notifications.ToListAsync());
    }
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public class RecoverPasswordRequestValidatorTests
{
    private readonly RecoverPasswordRequestValidator _validator = new();

    [Fact]
    public void Needs_a_token_a_password_in_range_and_a_matching_confirmation()
    {
        Assert.True(_validator.Validate(new RecoverPasswordRequest("t", "longenough1", "longenough1")).IsValid);
        Assert.False(_validator.Validate(new RecoverPasswordRequest("", "longenough1", "longenough1")).IsValid);
        Assert.False(_validator.Validate(new RecoverPasswordRequest("t", "short", "short")).IsValid);
        Assert.False(_validator.Validate(new RecoverPasswordRequest("t", new string('x', 129), new string('x', 129))).IsValid);
        Assert.False(_validator.Validate(new RecoverPasswordRequest("t", "longenough1", "different1")).IsValid);
    }
}
