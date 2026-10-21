using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>The processing half of Requirement 18: <c>IUserService.SetPasswordAsync</c>, the
/// administrator's process and retry actions (design D8's two-transaction order, the claim guard, the
/// honest Failed status), the audit trail's no-secret guarantee and the new in-app notification of
/// type <see cref="NotificationType.PasswordReset"/>.</summary>
public class PasswordResetProcessingTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string IssuedPassword = "Adm1n-Issued#1";
    private const string RecoverySubject = "Your TaskFlow password has been reset";

    private readonly TaskFlowApiFactory _factory;

    public PasswordResetProcessingTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ---- 3.1 SetPasswordAsync ---------------------------------------------------------------

    [Fact]
    public async Task SetPasswordAsync_WithACompliantPassword_UpdatesTheHashAndEndsEverySession()
    {
        var username = Unique("set-ok");
        var actingAdmin = Unique("set-ok-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        var original = await db.Users.SingleAsync(user => user.Id == userId);
        var originalHash = original.PasswordHash;
        var originalStamp = original.SecurityStamp;

        // One live session of the account whose password is about to change.
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = "seeded-session-hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        const string newPassword = "N3wP@ssword!";
        var changed = await userService.SetPasswordAsync(userId, newPassword, adminId);

        Assert.True(changed);
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, newPassword));
        Assert.Equal(PasswordVerificationResult.Failed, await VerifyAsync(username, Password));

        db.ChangeTracker.Clear();
        var updated = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
        Assert.NotEqual(originalHash, updated.PasswordHash);
        Assert.NotEqual(originalStamp, updated.SecurityStamp);

        var session = await db.RefreshTokens.AsNoTracking().SingleAsync(token => token.UserId == userId);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task SetPasswordAsync_WithATooShortPassword_IsRefusedLeavingTheHashUnchanged()
    {
        var username = Unique("set-short");
        var actingAdmin = Unique("set-short-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        var original = await db.Users.SingleAsync(user => user.Id == userId);
        var originalHash = original.PasswordHash;
        var originalStamp = original.SecurityStamp;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = "seeded-session-hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => userService.SetPasswordAsync(userId, "short", adminId));

        // Nothing moved: hash, security stamp and the live session all stay as they were.
        db.ChangeTracker.Clear();
        var unchanged = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
        Assert.Equal(originalHash, unchanged.PasswordHash);
        Assert.Equal(originalStamp, unchanged.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, Password));

        var session = await db.RefreshTokens.AsNoTracking().SingleAsync(token => token.UserId == userId);
        Assert.Null(session.RevokedAt);
    }

    // ---- 3.3 The process endpoint ------------------------------------------------------------
    [Fact]
    public async Task ProcessLoginScreenRequest_SetsTheAdministratorsPassword_TellsTheUserWithoutIt_AndCompletes()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-login");
        var actingAdmin = Unique("proc-login-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        var adminToken = await LoginAsync(client, actingAdmin);
        var response = await ProcessAsync(client, request.ReferenceId, adminToken, IssuedPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();

        // The password is in no response, mail or notification: the administrator relays it.
        Assert.Contains("Completed", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(IssuedPassword, responseBody, StringComparison.Ordinal);

        var recovery = await GetRecoveryMailAsync(userId);
        Assert.NotNull(recovery);
        Assert.DoesNotContain(IssuedPassword, recovery.Body, StringComparison.Ordinal);
        Assert.Contains("Ask your administrator", recovery.Body, StringComparison.Ordinal);
        Assert.Contains("You must choose a new password", recovery.Body, StringComparison.Ordinal);

        // The request row records who did what, when - with no failure anywhere.
        var processed = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Completed, processed.Status);
        Assert.True(processed.TemporaryPasswordIssued);
        Assert.NotNull(processed.CompletedAt);
        Assert.NotNull(processed.UserNotifiedAt);
        Assert.Equal(adminId, processed.ProcessedByUserId);
        Assert.Null(processed.FailureReason);

        // The administrator's password is the credential, the old one is dead, the change is owed.
        Assert.Equal(PasswordVerificationResult.Failed, await VerifyAsync(username, Password));
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, IssuedPassword));
        Assert.True((await GetUserAsync(username)).MustChangePassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var notification = await db.Notifications.SingleAsync(
            item => item.UserId == userId && item.Type == NotificationType.PasswordReset);
        Assert.Contains("Ask your administrator", notification.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(IssuedPassword, notification.Message, StringComparison.Ordinal);

        // Login-Screen origin: the per-event login notice exists and starts out uncleared.
        var notice = await db.LoginPasswordResetNotices.SingleAsync(item => item.UserId == userId);
        Assert.Null(notice.ClearedAt);
        Assert.Equal(request.Id, notice.PasswordResetRequestId);
    }

    [Fact]
    public async Task ProcessProfileScreenRequest_SetsTheAdministratorsPassword_AndForcesAChange()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-profile");
        var actingAdmin = Unique("proc-profile-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        var userToken = await LoginAsync(client, username);
        var created = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password-reset-request", userToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var request = await GetRequestAsync(username);
        Assert.Equal(PasswordResetOrigin.ProfileScreen, request.Origin);

        var adminToken = await LoginAsync(client, actingAdmin);
        var response = await ProcessAsync(client, request.ReferenceId, adminToken, IssuedPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The supplied password is the credential and, because the administrator knows it, a change is owed.
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, IssuedPassword));
        Assert.Equal(PasswordVerificationResult.Failed, await VerifyAsync(username, Password));
        Assert.True((await GetUserAsync(username)).MustChangePassword);

        var processed = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Completed, processed.Status);
        Assert.Equal(adminId, processed.ProcessedByUserId);

        // The password appears nowhere in the user's recovery mail or notification.
        var recovery = await GetRecoveryMailAsync(userId);
        Assert.NotNull(recovery);
        Assert.DoesNotContain(IssuedPassword, recovery.Body, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.Notifications.SingleAsync(
            item => item.UserId == userId && item.Type == NotificationType.PasswordReset);
        Assert.DoesNotContain(IssuedPassword, notification.Message, StringComparison.Ordinal);

        // Profile-Screen origins write the login notice too: the requester was signed out by the reset.
        var notice = await db.LoginPasswordResetNotices.SingleAsync(item => item.UserId == userId);
        Assert.Null(notice.ClearedAt);
        Assert.Equal(request.Id, notice.PasswordResetRequestId);
    }

    [Fact]
    public async Task ProcessWithoutAPassword_IsRejected_AndTheRequestStaysPending()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-missing");
        var actingAdmin = Unique("proc-missing-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        var adminToken = await LoginAsync(client, actingAdmin);
        foreach (var missing in new string?[] { null, "", "   " })
        {
            var response = await ProcessAsync(client, request.ReferenceId, adminToken, missing);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, Password));
        var unchanged = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Pending, unchanged.Status);
        Assert.Null(unchanged.ProcessedByUserId);
        Assert.False((await GetUserAsync(username)).MustChangePassword);
        Assert.Equal(0, await CountRecoveryMailsAsync(userId));
    }

    [Fact]
    public async Task ProcessWithAWeakSuppliedPassword_IsRejected_AndChangesNothing()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-weak");
        var actingAdmin = Unique("proc-weak-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);

        var userToken = await LoginAsync(client, username);
        var created = await SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password-reset-request", userToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var request = await GetRequestAsync(username);

        var adminToken = await LoginAsync(client, actingAdmin);
        var response = await ProcessAsync(client, request.ReferenceId, adminToken, "abc");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Password", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Password untouched, request untouched, user told nothing.
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, Password));
        var unchanged = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Pending, unchanged.Status);
        Assert.Null(unchanged.CompletedAt);
        Assert.Null(unchanged.ProcessedByUserId);
        Assert.Null(unchanged.UserNotifiedAt);
        Assert.False(unchanged.TemporaryPasswordIssued);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Notifications.AnyAsync(
            item => item.UserId == userId && item.Type == NotificationType.PasswordReset));
        Assert.Equal(0, await CountRecoveryMailsAsync(userId));
    }

    [Fact]
    public async Task ProcessWithAMailWriteFailure_ReportsAnHonest502_KeepsThePasswordChange_AndMarksFailed()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-mailfail");
        var actingAdmin = Unique("proc-mailfail-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);
        var adminToken = await LoginAsync(client, actingAdmin);

        using (var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemMailWriter>();
            services.AddSingleton<ISystemMailWriter>(new ThrowingMailWriter());
        })))
        {
            var failingClient = failing.CreateClient();
            var response = await ProcessAsync(failingClient, request.ReferenceId, adminToken, password: IssuedPassword);

            // An honest failure: the mail half could not be written, so the API says so - never a success.
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Failed", responseBody, StringComparison.Ordinal);
            Assert.Contains("Simulated system mail write failure.", responseBody, StringComparison.Ordinal);
        }

        var processed = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Failed, processed.Status);
        Assert.Contains(
            "Simulated system mail write failure.",
            processed.FailureReason!,
            StringComparison.Ordinal);
        Assert.Null(processed.CompletedAt);
        Assert.Null(processed.UserNotifiedAt);
        Assert.True(processed.TemporaryPasswordIssued);
        Assert.Equal(adminId, processed.ProcessedByUserId);

        // The password change from the first half stays: reverting would restore the forgotten password.
        Assert.Equal(PasswordVerificationResult.Failed, await VerifyAsync(username, Password));
        Assert.True((await GetUserAsync(username)).MustChangePassword);

        // But the user was told nothing: no mail, no in-app notification, no login notice.
        Assert.Equal(0, await CountRecoveryMailsAsync(userId));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Notifications.AnyAsync(
            item => item.UserId == userId && item.Type == NotificationType.PasswordReset));
        Assert.False(await db.LoginPasswordResetNotices.AnyAsync(item => item.UserId == userId));
    }

    [Fact]
    public async Task ASecondAdministrator_ProcessingTheSameRequest_IsRefusedAsAlreadyProcessed()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-second");
        var firstAdmin = Unique("proc-second-1");
        var secondAdmin = Unique("proc-second-2");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(firstAdmin, $"{firstAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(secondAdmin, $"{secondAdmin}@taskflow.com", Password, SystemRoles.Administrator);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        var firstToken = await LoginAsync(client, firstAdmin);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessAsync(client, request.ReferenceId, firstToken, password: IssuedPassword)).StatusCode);
        var hashAfterFirst = (await GetUserAsync(username)).PasswordHash;

        var secondToken = await LoginAsync(client, secondAdmin);
        var second = await ProcessAsync(client, request.ReferenceId, secondToken, password: IssuedPassword);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(
            "already been completed",
            await second.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        // The refused attempt changed nothing.
        Assert.Equal(hashAfterFirst, (await GetUserAsync(username)).PasswordHash);
    }

    [Fact]
    public async Task Process_WhenAnotherAdministratorHasAlreadyClaimedTheRequest_IsRefusedByTheClaimGuard()
    {
        var client = _factory.CreateClient();
        var username = Unique("proc-claim");
        var actingAdmin = Unique("proc-claim-1");
        var claimingAdmin = Unique("proc-claim-2");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(claimingAdmin, $"{claimingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var claimingAdminId = await GetUserIdAsync(claimingAdmin);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        // A second administrator mid-claim: the conditional update has marked the row while the status
        // is still Pending (design D8 marks Completed only after the notification), which is exactly
        // the state a concurrent process loses the race in.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PasswordResetRequests.SingleAsync(item => item.Id == request.Id);
            row.ProcessedByUserId = claimingAdminId;
            await db.SaveChangesAsync();
        }

        var adminToken = await LoginAsync(client, actingAdmin);
        var response = await ProcessAsync(client, request.ReferenceId, adminToken, password: IssuedPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "Another administrator has already processed this request.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        // The guard refused the attempt before anything happened: password, status and claim intact.
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, Password));
        var unchanged = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Pending, unchanged.Status);
        Assert.Equal(claimingAdminId, unchanged.ProcessedByUserId);
        Assert.Equal(0, await CountRecoveryMailsAsync(userId));
    }

    // ---- 3.4 Retry notification -----------------------------------------------------------------

    [Fact]
    public async Task RetryAfterANotificationFailure_CompletesTheRequest_WithoutChangingThePasswordOrReissuing()
    {
        var client = _factory.CreateClient();
        var username = Unique("retry-user");
        var actingAdmin = Unique("retry-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);
        var adminToken = await LoginAsync(client, actingAdmin);

        // First attempt: the password half lands, the user's mail half fails.
        using (var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemMailWriter>();
            services.AddSingleton<ISystemMailWriter>(new ThrowingMailWriter());
        })))
        {
            var failingClient = failing.CreateClient();
            var failed = await ProcessAsync(failingClient, request.ReferenceId, adminToken, password: IssuedPassword);
            Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        }

        var failure = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Failed, failure.Status);
        Assert.True(failure.TemporaryPasswordIssued);
        var hashAfterFailure = (await GetUserAsync(username)).PasswordHash;
        Assert.Equal(PasswordVerificationResult.Failed, await VerifyAsync(username, Password));

        // The retry re-runs the notification half only.
        var retry = await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/password-reset-requests/{request.ReferenceId}/retry-notification",
            adminToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("Completed", await retry.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The password was not re-hashed on the retry.
        Assert.Equal(hashAfterFailure, (await GetUserAsync(username)).PasswordHash);
        var completed = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Completed, completed.Status);
        Assert.Equal(failure.TemporaryPasswordIssued, completed.TemporaryPasswordIssued);
        Assert.NotNull(completed.CompletedAt);
        Assert.NotNull(completed.UserNotifiedAt);
        Assert.Equal(adminId, completed.ProcessedByUserId);
        Assert.Null(completed.FailureReason);
        Assert.True((await GetUserAsync(username)).MustChangePassword);

        // The retry mail says the same as the first would have: ask the administrator, and carries no password.
        var recovery = await GetRecoveryMailAsync(userId);
        Assert.NotNull(recovery);
        Assert.Contains("Ask your administrator", recovery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(IssuedPassword, recovery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Your temporary password is:", recovery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, recovery.Body, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Notifications.AnyAsync(
            item => item.UserId == userId && item.Type == NotificationType.PasswordReset));
        Assert.True(await db.LoginPasswordResetNotices.AnyAsync(
            item => item.UserId == userId && item.ClearedAt == null));

        // A request that is no longer Failed cannot be retried again.
        var again = await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/password-reset-requests/{request.ReferenceId}/retry-notification",
            adminToken);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(
            "Only a request whose notification failed can be retried.",
            await again.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    // ---- 3.5 Audit trail -------------------------------------------------------------------------

    [Fact]
    public async Task TheRecoveryLifecycle_WritesEveryAuditEvent_AndNeverALineContainingAPasswordOrHash()
    {
        var client = _factory.CreateClient();
        var username = Unique("audit-user");
        var actingAdmin = Unique("audit-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);
        var adminId = await GetUserIdAsync(actingAdmin);

        // 1. A denied attempt: the submitted identifier only, never a credential.
        var missingIdentifier = Unique("audit-missing");
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, missingIdentifier)).StatusCode);

        // 2. Request created and the administrators notified.
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        // 3. Password set, user notified, login notice set.
        var adminToken = await LoginAsync(client, actingAdmin);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessAsync(client, request.ReferenceId, adminToken, password: IssuedPassword)).StatusCode);

        var temporaryPassword = IssuedPassword;
        var passwordHash = (await GetUserAsync(username)).PasswordHash;

        var lines = _factory.Logs.Lines;

        Assert.Contains(lines, line => line.Contains("denied: no active account matches", StringComparison.Ordinal)
            && line.Contains(missingIdentifier, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(
            $"Password recovery request {request.ReferenceId} created for user {userId}", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(
            $"Administrator notification mail written for password recovery request {request.ReferenceId}",
            StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(
            $"Password set for user {userId} on recovery request {request.ReferenceId} by administrator {adminId}",
            StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(
            $"User {userId} notified of password recovery request {request.Id}", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(
            $"Login credential notice set for user {userId} from password recovery request {request.Id}",
            StringComparison.Ordinal));

        // And no captured line ever carries a password or a hash: not the administrator-typed password...
        Assert.DoesNotContain(lines, line => line.Contains(temporaryPassword, StringComparison.Ordinal));
        // ...not the account's stored hash...
        Assert.DoesNotContain(lines, line => line.Contains(passwordHash, StringComparison.Ordinal));
        // ...and not the password every seeded account shares.
        Assert.DoesNotContain(lines, line => line.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACallerWithoutThePermission_IsRefusedAndTheRefusalIsRecordedAsADeniedAction()
    {
        var client = _factory.CreateClient();
        var username = Unique("denied-user");
        var actingAdmin = Unique("denied-admin");
        var employee = Unique("denied-employee");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync(employee, $"{employee}@taskflow.com", Password, SystemRoles.Employee);
        var employeeId = await GetUserIdAsync(employee);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);

        // An authenticated caller whose role lacks employees.manage attempts the password change.
        var employeeToken = await LoginAsync(client, employee);
        var response = await ProcessAsync(client, request.ReferenceId, employeeToken, password: IssuedPassword);

        // Refused...
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // ...and recorded as a denied action, with identifiers only: who, what, where, under which
        // policy - never a credential (spec scenario "Unauthorized change attempt").
        var expectedPolicy = PermissionPolicy.NameFor(PermissionKeys.EmployeesManage);
        Assert.Contains(_factory.Logs.Lines, line =>
            line.Contains("Denied action:", StringComparison.Ordinal)
            && line.Contains($"user {employeeId}", StringComparison.Ordinal)
            && line.Contains(request.ReferenceId.ToString(), StringComparison.Ordinal)
            && line.Contains(expectedPolicy, StringComparison.Ordinal));

        // The refused attempt changed nothing: the request is still Pending and the password intact.
        var unchanged = await GetRequestAsync(username);
        Assert.Equal(PasswordResetStatus.Pending, unchanged.Status);
        Assert.Equal(PasswordVerificationResult.Success, await VerifyAsync(username, Password));
        Assert.Equal(0, await CountRecoveryMailsAsync(await GetUserIdAsync(username)));
    }

    // ---- 3.6 Backend half of the notification type ---------------------------------------------

    [Fact]
    public async Task ThePasswordResetNotification_IsReturnedWithTypePasswordReset()
    {
        var client = _factory.CreateClient();
        var username = Unique("notif-user");
        var actingAdmin = Unique("notif-admin");
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync(actingAdmin, $"{actingAdmin}@taskflow.com", Password, SystemRoles.Administrator);
        var userId = await GetUserIdAsync(username);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(client, username)).StatusCode);
        var request = await GetRequestAsync(username);
        var adminToken = await LoginAsync(client, actingAdmin);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessAsync(client, request.ReferenceId, adminToken, password: IssuedPassword)).StatusCode);

        // The user signs in with the password the administrator relayed and reads their own notifications.
        var temporaryPassword = IssuedPassword;
        var temporaryToken = await LoginAsync(client, username, temporaryPassword);

        // The temporary password forces a change first (design D7); only then are the other routes open.
        const string chosenPassword = "Chosen-P@ss1";
        using (var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = temporaryPassword, newPassword = chosenPassword })
        })
        {
            change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", temporaryToken);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(change)).StatusCode);
        }

        var userToken = await LoginAsync(client, username, chosenPassword);

        var filtered = await SendAsync(
            client, HttpMethod.Get, "/api/v1/notifications?type=PasswordReset", userToken);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);

        using var document = await JsonDocument.ParseAsync(await filtered.Content.ReadAsStreamAsync());
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("PasswordReset", item.GetProperty("type").GetString());
        Assert.Contains(
            "Ask your administrator",
            item.GetProperty("message").GetString(),
            StringComparison.Ordinal);

        // The filter is a real filter: another type never carries this notification.
        var otherType = await SendAsync(
            client, HttpMethod.Get, "/api/v1/notifications?type=System", userToken);
        using var otherDocument = await JsonDocument.ParseAsync(await otherType.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(
            otherDocument.RootElement.GetProperty("items").EnumerateArray(),
            element => element.GetProperty("type").GetString() == "PasswordReset");
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SubmitAsync(HttpClient client, string? identifier) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/password-reset-requests",
            new Dictionary<string, string?> { ["identifier"] = identifier });

    private async Task<HttpResponseMessage> ProcessAsync(
        HttpClient client,
        Guid referenceId,
        string adminToken,
        string? password)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/password-reset-requests/{referenceId}/process")
        {
            Content = JsonContent.Create(new Dictionary<string, string?> { ["password"] = password })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        return await client.SendAsync(request);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password
        });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users
            .Where(user => user.Username == username)
            .Select(user => user.Id)
            .SingleAsync();
    }

    private async Task<User> GetUserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().SingleAsync(user => user.Username == username);
    }

    private async Task<PasswordResetRequest> GetRequestAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PasswordResetRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleAsync(request => request.User!.Username == username);
    }

    private async Task<PasswordVerificationResult> VerifyAsync(string username, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Username == username);
        return hasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    /// <summary>The user's newest recovery mail (the temporary password exists only there).</summary>
    private async Task<MailMessage?> GetRecoveryMailAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inboxMessageIds = await db.MailboxItems
            .Where(item => item.OwnerUserId == userId && item.Folder == MailFolder.Inbox)
            .Select(item => item.MessageId)
            .ToListAsync();

        return await db.MailMessages
            .Where(message => message.Subject == RecoverySubject && inboxMessageIds.Contains(message.Id))
            .OrderByDescending(message => message.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountRecoveryMailsAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inboxMessageIds = await db.MailboxItems
            .Where(item => item.OwnerUserId == userId && item.Folder == MailFolder.Inbox)
            .Select(item => item.MessageId)
            .ToListAsync();

        return await db.MailMessages
            .CountAsync(message => message.Subject == RecoverySubject && inboxMessageIds.Contains(message.Id));
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Fails every system mail write, to prove the honest Failed status and the retry.</summary>
    private sealed class ThrowingMailWriter : ISystemMailWriter
    {
        public Task WriteAsync(
            IReadOnlyCollection<int> recipientUserIds,
            string subject,
            string body,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated system mail write failure.");
    }
}
