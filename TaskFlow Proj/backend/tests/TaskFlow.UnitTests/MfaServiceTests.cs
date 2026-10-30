using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Mfa;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

/// <summary>Authenticator MFA rules: enrolment, replay protection, lockout, backup codes, turning off and reset.</summary>
public class MfaServiceTests : IAsyncDisposable
{
    private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 30);
    private const string Password = "P@ssw0rd!";

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly FixedTimeProvider _clock = new(Start);
    private readonly MfaSecretProtector _protector;
    private readonly MfaOptions _options = new();
    private readonly MfaService _service;
    private readonly User _user;

    public MfaServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);

        _protector = new MfaSecretProtector(new EphemeralDataProtectionProvider(), _clock);
        _service = new MfaService(_db, _hasher, _protector, new NotificationPublisher(_db, _clock), Options.Create(_options), _clock);

        _user = new User
        {
            FullName = "Mfa User",
            Username = "mfa-user",
            Email = "mfa-user@taskflow.com",
            RoleId = SystemRoles.EmployeeId,
            SecurityStamp = "stamp-before",
            IsActive = true,
            CreatedAt = Start,
            UpdatedAt = Start
        };
        _user.PasswordHash = _hasher.HashPassword(_user, Password);
        _db.Users.Add(_user);
        _db.SaveChanges();
    }

    private byte[] SecretFrom(MfaSetupResult setup) => Base32.Decode(setup.Key)!;

    private string CodeNow(byte[] secret, int stepOffset = 0) =>
        Totp.CodeFor(secret, Totp.StepFor(new DateTimeOffset(DateTime.SpecifyKind(_clock.GetUtcNow().UtcDateTime, DateTimeKind.Utc))) + stepOffset);

    private async Task<(byte[] Secret, IReadOnlyList<string> BackupCodes)> EnableAsync()
    {
        var setup = (await _service.BeginSetupAsync(_user.Id, Password))!;
        var secret = SecretFrom(setup);
        var codes = (await _service.EnableAsync(_user.Id, CodeNow(secret)))!;
        _clock.Set(_clock.GetUtcNow().UtcDateTime.AddSeconds(31)); // next step, so the enrolment code is behind us
        return (secret, codes);
    }

    private Task<User> ReloadAsync() => _db.Users.AsNoTracking().SingleAsync(u => u.Id == _user.Id);

    // ------------------------------------------------------------------ enrolment

    [Fact]
    public async Task Setup_needs_the_right_password_and_leaves_mfa_off()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BeginSetupAsync(_user.Id, "wrong"));
        Assert.Null((await ReloadAsync()).MfaSecretProtected);

        var setup = await _service.BeginSetupAsync(_user.Id, Password);

        var stored = await ReloadAsync();
        Assert.NotNull(setup);
        Assert.Null(stored.MfaEnabledAt);
        Assert.NotNull(stored.MfaSecretProtected);
        Assert.DoesNotContain(setup!.Key, stored.MfaSecretProtected);
        Assert.StartsWith("otpauth://totp/TaskFlow:mfa-user%40taskflow.com", setup.ProvisioningUri);
    }

    [Fact]
    public async Task A_second_setup_replaces_an_unconfirmed_key()
    {
        var first = (await _service.BeginSetupAsync(_user.Id, Password))!;
        var second = (await _service.BeginSetupAsync(_user.Id, Password))!;

        Assert.NotEqual(first.Key, second.Key);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EnableAsync(_user.Id, CodeNow(SecretFrom(first))));
        Assert.NotNull(await _service.EnableAsync(_user.Id, CodeNow(SecretFrom(second))));
    }

    [Fact]
    public async Task Enabling_with_a_wrong_code_keeps_mfa_off()
    {
        var setup = (await _service.BeginSetupAsync(_user.Id, Password))!;
        var code = CodeNow(SecretFrom(setup));
        var wrong = code == "000000" ? "111111" : "000000";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EnableAsync(_user.Id, wrong));

        Assert.Equal(MfaService.InvalidCodeMessage, error.Message);
        Assert.Null((await ReloadAsync()).MfaEnabledAt);
    }

    [Fact]
    public async Task Enabling_with_a_valid_code_turns_mfa_on_and_returns_ten_hashed_backup_codes()
    {
        var (_, codes) = await EnableAsync();

        var stored = await ReloadAsync();
        Assert.NotNull(stored.MfaEnabledAt);
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[A-Z2-7]{5}-[A-Z2-7]{5}$", code));

        var hashes = await _db.MfaBackupCodes.Select(row => row.CodeHash).ToListAsync();
        Assert.Equal(10, hashes.Count);
        Assert.All(codes, code => Assert.DoesNotContain(code.Replace("-", ""), hashes));
    }

    [Fact]
    public async Task Setup_is_refused_while_mfa_is_already_on()
    {
        await EnableAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BeginSetupAsync(_user.Id, Password));
    }

    // ------------------------------------------------------------------ verification

    [Fact]
    public async Task A_valid_code_is_accepted_once_and_never_replayed()
    {
        var (secret, _) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);
        var code = CodeNow(secret);

        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, code, allowBackupCode: true));
        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, code, allowBackupCode: true));
    }

    [Fact]
    public async Task The_previous_step_is_accepted_but_an_older_code_is_not()
    {
        var (secret, _) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);
        _clock.Set(_clock.GetUtcNow().UtcDateTime.AddSeconds(60));

        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, CodeNow(secret, -2), true));
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, CodeNow(secret, -1), true));
    }

    [Fact]
    public async Task Five_wrong_codes_lock_verification_even_for_a_correct_code_until_the_lock_ends()
    {
        var (secret, _) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);
        var correct = CodeNow(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, wrong, true));
        }

        Assert.Equal(MfaVerifyOutcome.LockedOut, await _service.VerifyAsync(user, correct, true));

        _clock.Set(_clock.GetUtcNow().UtcDateTime.AddMinutes(5).AddSeconds(1));
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, CodeNow(secret), true));
    }

    [Fact]
    public async Task A_correct_code_resets_the_failure_count()
    {
        var (secret, _) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);
        var wrong = CodeNow(secret) == "000000" ? "111111" : "000000";

        for (var i = 0; i < 4; i++)
        {
            await _service.VerifyAsync(user, wrong, true);
        }

        Assert.Equal(4, user.MfaFailedAttempts);
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, CodeNow(secret), true));
        Assert.Equal(0, user.MfaFailedAttempts);
        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, wrong, true));
        Assert.Equal(1, user.MfaFailedAttempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task Malformed_codes_are_invalid(string? code)
    {
        await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);

        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, code, true));
    }

    // ------------------------------------------------------------------ backup codes

    [Fact]
    public async Task A_backup_code_works_once_in_any_case_with_or_without_the_dash()
    {
        var (_, codes) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);

        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, codes[0].ToLowerInvariant().Replace("-", ""), true));
        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, codes[0], true));
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, codes[1], true));
        Assert.Equal(8, await _db.MfaBackupCodes.CountAsync(row => row.UsedAt == null));
    }

    [Fact]
    public async Task A_backup_code_is_refused_where_only_an_authenticator_code_is_allowed()
    {
        var (_, codes) = await EnableAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);

        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, codes[0], allowBackupCode: false));
        Assert.Equal(10, await _db.MfaBackupCodes.CountAsync(row => row.UsedAt == null));
    }

    [Fact]
    public async Task Regenerating_replaces_the_set_and_needs_a_valid_code()
    {
        var (secret, codes) = await EnableAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegenerateBackupCodesAsync(_user.Id, "wrong", CodeNow(secret)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegenerateBackupCodesAsync(_user.Id, Password, "000000"));

        var fresh = (await _service.RegenerateBackupCodesAsync(_user.Id, Password, CodeNow(secret)))!;

        var user = await _db.Users.SingleAsync(u => u.Id == _user.Id);
        Assert.Equal(10, fresh.Count);
        Assert.Empty(fresh.Intersect(codes));
        Assert.Equal(10, await _db.MfaBackupCodes.CountAsync());
        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(user, codes[0], true));
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(user, fresh[0], true));
    }

    // ------------------------------------------------------------------ turning off and reset

    [Fact]
    public void Nobody_can_turn_mfa_off_by_themselves()
    {
        Assert.DoesNotContain(typeof(IMfaService).GetMethods(), method => method.Name.Contains("Disable", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------ step-up for sensitive actions

    [Fact]
    public async Task A_sensitive_action_needs_a_valid_code_and_spends_it()
    {
        var (secret, _) = await EnableAsync();

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, null));
        Assert.Equal(MfaService.CodeRequiredMessage, missing.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, "000000"));

        var code = CodeNow(secret);
        await _service.RequireCodeAsync(_user.Id, code);

        var replay = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, code));
        Assert.Equal(MfaService.InvalidCodeMessage, replay.Message);
    }

    [Fact]
    public async Task A_backup_code_also_satisfies_a_sensitive_action_once()
    {
        var (_, codes) = await EnableAsync();

        await _service.RequireCodeAsync(_user.Id, codes[0]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, codes[0]));
    }

    [Fact]
    public async Task A_user_without_mfa_is_not_asked_for_a_code()
    {
        await _service.RequireCodeAsync(_user.Id, null);
        await _service.RequireCodeAsync(_user.Id, "whatever");
    }

    [Fact]
    public async Task Too_many_wrong_codes_lock_the_sensitive_action()
    {
        var (secret, _) = await EnableAsync();
        var wrong = CodeNow(secret) == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, wrong));
        }

        var locked = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RequireCodeAsync(_user.Id, CodeNow(secret)));
        Assert.Equal(MfaService.LockedMessage, locked.Message);
    }

    [Fact]
    public async Task Checking_a_code_for_the_cosmetic_unlock_does_not_spend_it()
    {
        var (secret, _) = await EnableAsync();
        var code = CodeNow(secret);

        Assert.Equal(MfaVerifyOutcome.Success, await _service.CheckCodeAsync(_user.Id, code));
        Assert.Equal(MfaVerifyOutcome.Success, await _service.CheckCodeAsync(_user.Id, code));

        // The same code is still good for the real action that follows.
        await _service.RequireCodeAsync(_user.Id, code);
    }

    [Fact]
    public async Task Checking_refuses_a_backup_code_and_wrong_codes_count_toward_the_lockout()
    {
        var (secret, codes) = await EnableAsync();
        var wrong = CodeNow(secret) == "000000" ? "111111" : "000000";

        Assert.Equal(MfaVerifyOutcome.Invalid, await _service.CheckCodeAsync(_user.Id, codes[0]));
        Assert.Equal(10, await _db.MfaBackupCodes.CountAsync(row => row.UsedAt == null));

        for (var i = 0; i < 4; i++)
        {
            await _service.CheckCodeAsync(_user.Id, wrong);
        }

        Assert.Equal(MfaVerifyOutcome.LockedOut, await _service.CheckCodeAsync(_user.Id, CodeNow(secret)));
    }

    [Fact]
    public async Task Admin_reset_clears_mfa_ends_sessions_and_notifies_the_user()
    {
        await EnableAsync();
        var session = new RefreshToken { UserId = _user.Id, TokenHash = "h", ExpiresAt = Start.AddDays(7), CreatedAt = Start };
        _db.RefreshTokens.Add(session);
        await _db.SaveChangesAsync();

        Assert.True(await _service.ResetAsync(_user.Id));

        var stored = await ReloadAsync();
        Assert.Null(stored.MfaEnabledAt);
        Assert.Null(stored.MfaSecretProtected);
        Assert.NotEqual("stamp-before", stored.SecurityStamp);
        Assert.Empty(await _db.MfaBackupCodes.ToListAsync());
        Assert.NotNull((await _db.RefreshTokens.AsNoTracking().SingleAsync()).RevokedAt);
        Assert.Contains(await _db.Notifications.ToListAsync(), n => n.UserId == _user.Id && n.Message.Contains("reset your authenticator MFA"));
        Assert.False(await _service.ResetAsync(9999));
    }

    [Fact]
    public async Task The_same_code_cannot_be_spent_by_two_parallel_requests()
    {
        var (secret, _) = await EnableAsync();
        var code = CodeNow(secret);

        // A second request that loaded the user before the first one finished.
        await using var otherDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        var otherUser = await otherDb.Users.FirstAsync(candidate => candidate.Id == _user.Id);
        var otherService = new MfaService(otherDb, _hasher, _protector, new NotificationPublisher(otherDb, _clock), Options.Create(_options), _clock);

        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(_user, code, true));
        Assert.Equal(MfaVerifyOutcome.Invalid, await otherService.VerifyAsync(otherUser, code, true));
    }

    [Fact]
    public async Task A_backup_code_cannot_be_spent_by_two_parallel_requests()
    {
        var (_, codes) = await EnableAsync();

        await using var otherDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        var otherUser = await otherDb.Users.FirstAsync(candidate => candidate.Id == _user.Id);
        var otherService = new MfaService(otherDb, _hasher, _protector, new NotificationPublisher(otherDb, _clock), Options.Create(_options), _clock);

        // Both requests see the code as unused; only the first save may win.
        var firstRow = await _db.MfaBackupCodes.FirstAsync(row => row.UserId == _user.Id && row.UsedAt == null);
        Assert.NotNull(firstRow);
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(_user, codes[0], true));
        Assert.Equal(MfaVerifyOutcome.Invalid, await otherService.VerifyAsync(otherUser, codes[0], true));
    }

    [Fact]
    public async Task A_wrong_code_does_not_touch_the_failed_counter_when_the_caller_throttles_by_itself()
    {
        var (secret, _) = await EnableAsync();
        var wrong = CodeNow(secret) == "000000" ? "111111" : "000000";

        for (var i = 0; i < _options.MaxFailedAttempts + 2; i++)
        {
            Assert.Equal(MfaVerifyOutcome.Invalid, await _service.VerifyAsync(_user, wrong, allowBackupCode: false, countFailure: false));
        }

        var stored = await _db.Users.AsNoTracking().FirstAsync(candidate => candidate.Id == _user.Id);
        Assert.Equal(0, stored.MfaFailedAttempts);
        Assert.Null(stored.MfaLockedUntil);
        Assert.Equal(MfaVerifyOutcome.Success, await _service.VerifyAsync(_user, CodeNow(secret), true));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
