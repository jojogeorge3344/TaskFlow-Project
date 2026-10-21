using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Mail;
using TaskFlow.Application.PasswordReset;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Mail;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>The admin-assisted password recovery lifecycle: recording a request for a verified active
/// account and notifying every other administrator by internal mail, plus the administrator's
/// processing action (design D8: password first, then notify, then mark completed). No password -
/// current or new - is ever stored or logged here; the administrator-typed password is hashed by
/// IUserService.SetPasswordAsync and never mailed, notified or returned.</summary>
public sealed class PasswordResetService : IPasswordResetService
{
    private const int MaxFailureReasonLength = 500;

    private readonly AppDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ISystemMailWriter _mailWriter;
    private readonly TimeProvider _timeProvider;
    private readonly MailOptions _mailOptions;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly IPasswordResetLinkTokens _linkTokens;

    public PasswordResetService(
        AppDbContext dbContext,
        IUserService userService,
        INotificationPublisher notificationPublisher,
        ISystemMailWriter mailWriter,
        TimeProvider timeProvider,
        IOptions<MailOptions> mailOptions,
        ILogger<PasswordResetService> logger,
        IPasswordResetLinkTokens linkTokens)
    {
        _dbContext = dbContext;
        _userService = userService;
        _notificationPublisher = notificationPublisher;
        _mailWriter = mailWriter;
        _timeProvider = timeProvider;
        _mailOptions = mailOptions.Value;
        _logger = logger;
        _linkTokens = linkTokens;
    }

    public async Task<bool> CreateRequestAsync(
        int? sessionUserId,
        string? identifier,
        PasswordResetOrigin origin,
        CancellationToken cancellationToken = default)
    {
        var requester = sessionUserId is { } userId
            ? await _dbContext.Users
                .AsNoTracking()
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            : await FindActiveAccountAsync(identifier, cancellationToken);

        if (requester is null)
        {
            // Denied attempts are audited with the submitted identifier only - never any credential.
            _logger.LogInformation(
                "Password recovery request from {Origin} denied: no active account matches {Identifier}.",
                origin,
                (identifier ?? string.Empty).Trim());
            return false;
        }

        // One open request per account: repeated clicks must not pile up requests, because every processed request
        // overwrites the password and only the latest one works. The caller still gets the same confirmation.
        var openRequest = await _dbContext.PasswordResetRequests
            .FirstOrDefaultAsync(open => open.UserId == requester.Id && open.Status == PasswordResetStatus.Pending, cancellationToken);
        if (openRequest is not null)
        {
            // No second request, but the administrators are mailed again about the open one, so a missed or
            // failed first mail is never the end of it.
            _logger.LogInformation("Password recovery request from {Origin} matched the pending request for user {UserId}; administrators are notified again.", origin, requester.Id);
            await NotifyAdminsAsync(openRequest, requester, cancellationToken);
            return true;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new PasswordResetRequest
        {
            ReferenceId = Guid.NewGuid(),
            UserId = requester.Id,
            Origin = origin,
            Status = PasswordResetStatus.Pending,
            CreatedAt = now
        };

        _dbContext.PasswordResetRequests.Add(request);

        // The request is persisted before any notification attempt: a failed mail write must never
        // take the request with it, or the administrator would never see it (design D8).
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password recovery request {ReferenceId} created for user {UserId} from {Origin}.",
            request.ReferenceId,
            requester.Id,
            origin);

        await NotifyAdminsAsync(request, requester, cancellationToken);
        return true;
    }

    public async Task<PasswordResetLinkResult> CreateLoginLinkAsync(Guid referenceId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.PasswordResetRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ReferenceId == referenceId, cancellationToken);
        if (request is null)
        {
            return new(PasswordResetLinkOutcome.NotFound);
        }

        // Only the request that is the account's current notice event can yield a link, so a later reset
        // makes every earlier link unobtainable as well as dead, and once the user has signed in or updated (the
        // notice is cleared) there is nothing left to link to (design D14).
        // (kept as one statement below)
        var isLatestEvent = await _dbContext.LoginPasswordResetNotices
            .AsNoTracking()
            .AnyAsync(
                notice => notice.UserId == request.UserId
                    && notice.PasswordResetRequestId == request.Id
                    && notice.ClearedAt == null,
                cancellationToken);
        if (request.Status != PasswordResetStatus.Completed || !isLatestEvent)
        {
            return new(PasswordResetLinkOutcome.NotLatestCompleted);
        }

        return new(PasswordResetLinkOutcome.Ok, _linkTokens.Create(request.UserId, request.Id));
    }

    public async Task<string?> ResolveLoginLinkAsync(string? token, CancellationToken cancellationToken = default)
    {
        var named = _linkTokens.Read(token);
        if (named is null)
        {
            return null;
        }

        // The token must still name the account's current event; a later reset retires earlier links.
        return await _dbContext.LoginPasswordResetNotices
            .AsNoTracking()
            .Where(notice => notice.UserId == named.Value.UserId
                && notice.PasswordResetRequestId == named.Value.RequestId
                && notice.ClearedAt == null
                && notice.User!.IsActive)
            .Select(notice => notice.User!.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> CompleteFromLoginAsync(PasswordResetCompleteRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindAccountForLoginUpdateAsync(request.Token, cancellationToken);
        if (user is null || !user.MustChangePassword)
        {
            // No token, or one that is bad, expired, used or superseded: all the same refusal (design D14).
            _logger.LogWarning("Login-page password update denied: the link is missing, invalid, expired or already used.");
            return false;
        }

        try
        {
            // The valid link is the proof, so no current password is asked for. ChangeOwnPasswordAsync replaces the
            // hash, lifts the must-change flag and ends every session.
            await _userService.ChangeOwnPasswordAsync(
                user.Id,
                new ChangePasswordRequest(null, request.NewPassword ?? string.Empty),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning("Login-page password update refused for user {UserId}: {Reason}", user.Id, exception.Message);
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.LoginPasswordResetNotices
            .Where(notice => notice.UserId == user.Id && notice.ClearedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notice => notice.ClearedAt, now), cancellationToken);

        _logger.LogInformation("Password updated from the Login Screen for user {UserId}; the login notice is cleared.", user.Id);
        return true;
    }

    /// <summary>The account an encrypted login link names, if the link is still valid: unexpired, untampered, the
    /// account's current event and not yet used (the notice not cleared).</summary>
    private async Task<User?> FindAccountForLoginUpdateAsync(string? token, CancellationToken cancellationToken)
    {
        var named = _linkTokens.Read(token);
        if (named is null)
        {
            return null;
        }

        var isCurrentEvent = await _dbContext.LoginPasswordResetNotices
            .AsNoTracking()
            .AnyAsync(notice => notice.UserId == named.Value.UserId
                && notice.PasswordResetRequestId == named.Value.RequestId
                && notice.ClearedAt == null, cancellationToken);
        return isCurrentEvent
            ? await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == named.Value.UserId && user.IsActive, cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<PasswordResetRequestSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        // The IsDeleted query filter is ignored on purpose: a requester deleted after raising the
        // request must still appear in the administrator's list rather than silently vanishing.
        return await _dbContext.PasswordResetRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Select(request => new PasswordResetRequestSummary(
                request.ReferenceId,
                request.UserId,
                request.User!.FullName,
                request.User.Username,
                request.User.Email,
                request.User.Role.Name,
                request.Origin,
                request.Status,
                request.CreatedAt,
                request.CompletedAt,
                request.AdminNotifiedAt != null,
                request.UserNotifiedAt != null,
                request.TemporaryPasswordIssued,
                request.FailureReason))
            .ToListAsync(cancellationToken);
    }

    public async Task<PasswordResetProcessResult> ProcessAsync(
        Guid referenceId,
        string? suppliedPassword,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.PasswordResetRequests
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ReferenceId == referenceId, cancellationToken);

        if (request is null)
        {
            return new(PasswordResetProcessOutcome.NotFound, "No password recovery request carries that reference.");
        }

        if (request.Status != PasswordResetStatus.Pending)
        {
            return new(
                PasswordResetProcessOutcome.AlreadyProcessed,
                $"The request has already been {request.Status.ToString().ToLowerInvariant()}.");
        }

        var target = request.User;
        if (target is null || target.IsDeleted || !target.IsActive)
        {
            _logger.LogWarning(
                "Processing password recovery request {ReferenceId} refused: the requesting account is not active.",
                referenceId);
            return new(PasswordResetProcessOutcome.AccountUnavailable, "The requesting account is no longer active.");
        }

        // The administrator chooses the password for every request, whatever its origin (design D4/D6).
        if (string.IsNullOrWhiteSpace(suppliedPassword))
        {
            return new(PasswordResetProcessOutcome.InvalidPassword, "A new password is required.");
        }

        var password = suppliedPassword;

        // Transaction A: claim the request, change the password, set the change obligation (D8).
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The conditional update is the concurrency guard: whoever first claims an unclaimed
            // Pending request wins, and a second administrator processing the same request at the
            // same time matches no row and is told it is already processed. The claim itself keeps
            // the status Pending (D8 marks Completed only after the notification), so the
            // ProcessedByUserId IS NULL half is what makes the claim exclusive.
            var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "PasswordResetRequests" SET "ProcessedByUserId" = {actingAdminId} WHERE "Id" = {request.Id} AND "Status" = {PasswordResetStatus.Pending.ToString()} AND "ProcessedByUserId" IS NULL""",
                cancellationToken);
            if (claimed == 0)
            {
                return new(PasswordResetProcessOutcome.AlreadyProcessed, "Another administrator has already processed this request.");
            }

            var changed = await _userService.SetPasswordAsync(target.Id, password!, actingAdminId, cancellationToken);
            if (!changed)
            {
                return new(PasswordResetProcessOutcome.AccountUnavailable, "The requesting account is no longer active.");
            }

            // The administrator knows this password, so the user must replace it at first sign-in (D6).
            target.MustChangePassword = true;
            request.TemporaryPasswordIssued = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Password set for user {UserId} on recovery request {ReferenceId} by administrator {AdminId}; a change is required at next sign-in.",
                target.Id,
                referenceId,
                actingAdminId);
        }
        catch (InvalidOperationException exception)
        {
            // A policy refusal: the claim and the hash change roll back with the transaction, so the
            // request stays Pending and processable with a compliant password.
            _logger.LogWarning(
                "Processing password recovery request {ReferenceId} refused: {Reason}",
                referenceId,
                exception.Message);
            return new(PasswordResetProcessOutcome.InvalidPassword, exception.Message);
        }

        var notificationOutcome = await NotifyUserAndCompleteAsync(request.Id, target.Id, cancellationToken);
        if (notificationOutcome != PasswordResetProcessOutcome.Completed)
        {
            var failed = await _dbContext.PasswordResetRequests
                .AsNoTracking()
                .Where(r => r.Id == request.Id)
                .Select(r => r.FailureReason)
                .SingleAsync(cancellationToken);
            return new(notificationOutcome, failed);
        }

        return new(PasswordResetProcessOutcome.Completed);
    }

    public async Task<PasswordResetProcessResult> RetryNotificationAsync(
        Guid referenceId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.PasswordResetRequests
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ReferenceId == referenceId, cancellationToken);

        if (request is null)
        {
            return new(PasswordResetProcessOutcome.NotFound, "No password recovery request carries that reference.");
        }

        if (request.Status != PasswordResetStatus.Failed)
        {
            return new(
                PasswordResetProcessOutcome.AlreadyProcessed,
                "Only a request whose notification failed can be retried.");
        }

        var target = request.User;
        if (target is null || target.IsDeleted || !target.IsActive)
        {
            return new(PasswordResetProcessOutcome.AccountUnavailable, "The requesting account is no longer active.");
        }

        _logger.LogInformation(
            "Retrying the user notification for password recovery request {ReferenceId} by administrator {AdminId}.",
            referenceId,
            actingAdminId);

        // The retry re-runs the notification half only: no password is re-hashed (design D8) and
        // no mail ever carries one.
        var outcome = await NotifyUserAndCompleteAsync(request.Id, target.Id, cancellationToken);
        if (outcome != PasswordResetProcessOutcome.Completed)
        {
            var failed = await _dbContext.PasswordResetRequests
                .AsNoTracking()
                .Where(r => r.Id == request.Id)
                .Select(r => r.FailureReason)
                .SingleAsync(cancellationToken);
            return new(outcome, failed);
        }

        return new(PasswordResetProcessOutcome.Completed);
    }

    /// <summary>Transaction B of design D8: the user's recovery mail and in-app notification, the login
    /// notice, and only then Completed. Any failure rolls this half back and
    /// leaves the request Failed with a reason - the password stays changed either way, because
    /// reverting it would restore the password the user forgot.</summary>
    private async Task<PasswordResetProcessOutcome> NotifyUserAndCompleteAsync(
        int requestId,
        int targetUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var notification = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _mailWriter.WriteAsync(
                [targetUserId],
                BuildUserSubject(),
                BuildUserRecoveryBody(),
                cancellationToken);

            await _notificationPublisher.PublishAsync(
                targetUserId,
                null,
                NotificationType.PasswordReset,
                "An administrator has set a new password for your account. Ask your administrator for it; you must change it at your next sign-in.",
                cancellationToken);

            // Setting the password ended the requester's sessions, so a requester of either origin comes
            // back through the Login Screen and needs the notice there (design D2).
            await UpsertLoginNoticeAsync(requestId, targetUserId, cancellationToken);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var request = await _dbContext.PasswordResetRequests.SingleAsync(r => r.Id == requestId, cancellationToken);
            request.UserNotifiedAt = now;
            request.Status = PasswordResetStatus.Completed;
            request.CompletedAt = now;
            request.FailureReason = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await notification.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "User {UserId} notified of password recovery request {RequestId}; the request is completed.",
                targetUserId,
                requestId);

            return PasswordResetProcessOutcome.Completed;
        }
        catch (Exception exception)
        {
            await MarkNotificationFailedAsync(requestId, exception, cancellationToken);
            return PasswordResetProcessOutcome.NotificationFailed;
        }
    }

    /// <summary>Records that the user half failed without undoing the password change: the half-written
    /// mail/notification rows roll back, the change tracker is cleared so nothing half-written is
    /// retried, and only the request's status and reason are saved.</summary>
    private async Task MarkNotificationFailedAsync(int requestId, Exception exception, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var request = await _dbContext.PasswordResetRequests.SingleAsync(r => r.Id == requestId, cancellationToken);
        var reason = $"The user notification could not be written: {exception.Message}";
        request.Status = PasswordResetStatus.Failed;
        request.FailureReason = reason.Length > MaxFailureReasonLength ? reason[..MaxFailureReasonLength] : reason;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            exception,
            "The user notification for password recovery request {RequestId} failed; the request is marked Failed and stays retryable.",
            requestId);
    }

    /// <summary>Upserts the one-row-per-account login notice, rebinding it to the latest completed
    /// reset event so an earlier event's notice can never reappear (design D2).</summary>
    private async Task UpsertLoginNoticeAsync(int requestId, int targetUserId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var notice = await _dbContext.LoginPasswordResetNotices
            .FirstOrDefaultAsync(existing => existing.UserId == targetUserId, cancellationToken);

        if (notice is null)
        {
            notice = new LoginPasswordResetNotice
            {
                UserId = targetUserId,
                PasswordResetRequestId = requestId,
                CreatedAt = now
            };
            _dbContext.LoginPasswordResetNotices.Add(notice);
        }
        else
        {
            notice.PasswordResetRequestId = requestId;
            notice.CreatedAt = now;
            notice.ClearedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Login credential notice set for user {UserId} from password recovery request {RequestId}.",
            targetUserId,
            requestId);
    }

    private static string BuildUserSubject() => "Your TaskFlow password has been reset";

    /// <summary>The user's recovery mail. It never carries a password (design D4): the administrator chose
    /// it and passes it on outside the application, so the mail only says a new one was set.</summary>
    private static string BuildUserRecoveryBody() =>
        new System.Text.StringBuilder()
            .AppendLine("An administrator processed a password recovery request for your TaskFlow account.")
            .AppendLine()
            .AppendLine("A new password has been set. Ask your administrator for it; no password is ever sent by mail.")
            .AppendLine("You must choose a new password the first time you sign in with it.")
            .AppendLine()
            .AppendLine("If you did not request this change, contact your administrator immediately.")
            .Append("This is an automatic message from ").Append(MailRules.SystemSenderName).AppendLine(".")
            .ToString();

    /// <summary>The account the typed identifier belongs to, if it is active. The soft-delete filter on
    /// Users already excludes deleted accounts, so a deleted or unknown identifier resolves to null and
    /// nothing is written - while the caller still answers with the same generic confirmation.</summary>
    private Task<User?> FindActiveAccountAsync(string? identifier, CancellationToken cancellationToken)
    {
        var trimmed = (identifier ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Task.FromResult<User?>(null);
        }

        return _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .FirstOrDefaultAsync(
                user => (user.Username == trimmed || user.Email == trimmed) && user.IsActive,
                cancellationToken);
    }

    /// <summary>Writes the request mail to every active Administrator except the requester, as one system
    /// mail with one Inbox copy each, and stamps <see cref="PasswordResetRequest.AdminNotifiedAt"/>. When
    /// the write fails the request stays as it is with the stamp null: the list on the Employee Management
    /// screen - not the mail - is the source of truth (design D8), so the administrator still sees it.</summary>
    private async Task NotifyAdminsAsync(
        PasswordResetRequest request,
        User requester,
        CancellationToken cancellationToken)
    {
        var adminIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.RoleId == SystemRoles.AdministratorId
                && user.IsActive
                && user.Id != requester.Id)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        try
        {
            await _mailWriter.WriteAsync(
                adminIds,
                BuildSubject(requester),
                BuildBody(requester, request),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Administrator notification mail failed for password recovery request {ReferenceId}; the request remains listed with no notification stamp.",
                request.ReferenceId);
            return;
        }

        request.AdminNotifiedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Administrator notification mail written for password recovery request {ReferenceId} to {RecipientCount} administrator(s).",
            request.ReferenceId,
            adminIds.Count);
    }

    private static string BuildSubject(User requester) => $"Password reset request: {requester.Username}";

    /// <summary>The mail body: the requester's identification and the request details. It deliberately
    /// carries no password of any kind - the administrator sets the new password in the application.</summary>
    private string BuildBody(User requester, PasswordResetRequest request)
    {
        var body = new System.Text.StringBuilder()
            .AppendLine("A password recovery request has been raised and needs an administrator to set a new password.")
            .AppendLine()
            .Append("Full name: ").AppendLine(requester.FullName)
            .Append("Username: ").AppendLine(requester.Username)
            .Append("Email: ").AppendLine(requester.Email)
            .Append("Role: ").AppendLine(requester.Role.Name)
            .Append("Requested from: ").AppendLine(request.Origin switch
            {
                PasswordResetOrigin.ProfileScreen => "Profile Screen",
                _ => "Login Screen"
            })
            .Append("Reference: ").AppendLine(request.ReferenceId.ToString())
            .Append("Requested at: ")
            .Append(request.CreatedAt.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture))
            .AppendLine(" UTC")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(_mailOptions.AppBaseUrl))
        {
            // Deep link to this request on Employee Management (design D13). The reference is not a
            // credential; the screen still needs an administrator's session.
            body.Append("Open this request: ")
                .Append(_mailOptions.AppBaseUrl.TrimEnd('/'))
                .Append("/employees?resetRequest=")
                .AppendLine(request.ReferenceId.ToString());
        }

        body.AppendLine()
            .AppendLine("No password is included in this message; the new password is set inside the application.")
            .Append("This is an automatic message from ").Append(MailRules.SystemSenderName).AppendLine(".");

        return body.ToString();
    }
}
