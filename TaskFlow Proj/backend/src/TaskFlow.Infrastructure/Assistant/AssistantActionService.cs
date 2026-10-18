using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Assistant;

public class AssistantAuditLog : IAssistantAuditLog
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AssistantAuditLog(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(int userId, AssistantActionKind kind, string outcome, CancellationToken cancellationToken = default)
    {
        _dbContext.AssistantAudits.Add(new AssistantAudit
        {
            UserId = userId,
            Kind = kind,
            Outcome = outcome,
            At = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Pending actions and their confirmation. Nothing here is reachable by the model: it can only ask for an
/// action to be created, and an action only ever runs from <see cref="ConfirmAsync"/>, called by the user's own
/// request. The permission check runs again at that moment, with the user's current token.</summary>
public class AssistantActionService : IAssistantActionService
{
    private static readonly JsonSerializerOptions Json = AssistantService.MetaJson;

    private readonly AppDbContext _dbContext;
    private readonly IMailService _mail;
    private readonly IUserService _users;
    private readonly IAssistantAuthorizer _authorizer;
    private readonly IAssistantAuditLog _audit;
    private readonly IValidator<SendMailRequest> _sendMailValidator;
    private readonly IValidator<UpdateOwnProfileRequest> _profileValidator;
    private readonly TimeProvider _timeProvider;

    public AssistantActionService(
        AppDbContext dbContext,
        IMailService mail,
        IUserService users,
        IAssistantAuthorizer authorizer,
        IAssistantAuditLog audit,
        IValidator<SendMailRequest> sendMailValidator,
        IValidator<UpdateOwnProfileRequest> profileValidator,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _mail = mail;
        _users = users;
        _authorizer = authorizer;
        _audit = audit;
        _sendMailValidator = sendMailValidator;
        _profileValidator = profileValidator;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------------------------------- create

    public async Task<AssistantPendingActionDto> CreateAsync(
        AssistantCaller caller,
        int conversationId,
        AssistantActionKind kind,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var action = new AssistantPendingAction
        {
            Id = Guid.NewGuid(),
            UserId = caller.UserId,
            ConversationId = conversationId,
            Kind = kind,
            PayloadJson = JsonSerializer.Serialize(payload, payload.GetType(), Json),
            Status = AssistantActionStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + AssistantActionRules.Lifetime
        };
        _dbContext.AssistantPendingActions.Add(action);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(caller.UserId, kind, "prepared", cancellationToken);

        return ToCard(action, now);
    }

    // ------------------------------------------------------------------------------------------- cards

    public async Task<IReadOnlyDictionary<Guid, AssistantPendingActionDto>> GetCardsAsync(
        int userId,
        IReadOnlyCollection<Guid> actionIds,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var actions = await _dbContext.AssistantPendingActions
            .AsNoTracking()
            .Where(action => action.UserId == userId && actionIds.Contains(action.Id))
            .ToListAsync(cancellationToken);
        return actions.ToDictionary(action => action.Id, action => ToCard(action, now));
    }

    private static AssistantPendingActionDto ToCard(AssistantPendingAction action, DateTime now)
    {
        var status = action.Status == AssistantActionStatus.Pending && action.ExpiresAt <= now
            ? AssistantActionStatus.Expired
            : action.Status;
        var expiresAt = DateTime.SpecifyKind(action.ExpiresAt, DateTimeKind.Utc);

        switch (action.Kind)
        {
            case AssistantActionKind.SendMail:
                var mail = JsonSerializer.Deserialize<SendMailPayload>(action.PayloadJson, Json)!;
                return new AssistantPendingActionDto(
                    action.Id,
                    action.Kind,
                    status,
                    expiresAt,
                    "Mail draft",
                    [
                        new AssistantActionFieldDto("To", string.Join(", ", mail.To.Select(to => $"{to.FullName} <{to.Email}>"))),
                        new AssistantActionFieldDto("Subject", mail.Subject)
                    ],
                    mail.Body,
                    status == AssistantActionStatus.Pending);

            case AssistantActionKind.UpdateOwnProfile:
                var profile = JsonSerializer.Deserialize<UpdateProfilePayload>(action.PayloadJson, Json)!;
                var fields = new List<AssistantActionFieldDto>();
                if (profile.FullName is not null && profile.FullName != profile.CurrentFullName)
                {
                    fields.Add(new AssistantActionFieldDto("Name", $"{profile.CurrentFullName} → {profile.FullName}"));
                }

                if (profile.Email is not null && !string.Equals(profile.Email, profile.CurrentEmail, StringComparison.OrdinalIgnoreCase))
                {
                    fields.Add(new AssistantActionFieldDto("Email", $"{profile.CurrentEmail} → {profile.Email}"));
                }

                return new AssistantPendingActionDto(action.Id, action.Kind, status, expiresAt, "Update your profile", fields, null, false);

            default:
                var role = JsonSerializer.Deserialize<ChangeRolePayload>(action.PayloadJson, Json)!;
                return new AssistantPendingActionDto(
                    action.Id,
                    action.Kind,
                    status,
                    expiresAt,
                    "Change role",
                    [
                        new AssistantActionFieldDto("User", role.TargetName),
                        new AssistantActionFieldDto("Role", $"{role.CurrentRole} → {role.NewRole}")
                    ],
                    null,
                    false);
        }
    }

    // ------------------------------------------------------------------------------------------- edit and cancel

    public async Task<AssistantPendingActionDto?> EditMailAsync(
        AssistantCaller caller,
        Guid id,
        EditMailActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = await _dbContext.AssistantPendingActions
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == caller.UserId, cancellationToken);
        if (action is null)
        {
            return null;
        }

        var now = UtcNow;
        EnsurePending(action, now);
        if (action.Kind != AssistantActionKind.SendMail)
        {
            throw new AssistantRequestException("Only a mail can be edited.");
        }

        var current = JsonSerializer.Deserialize<SendMailPayload>(action.PayloadJson, Json)!;
        var addresses = request.To ?? current.To.Select(to => to.Email).ToList();
        var subject = request.Subject ?? current.Subject;
        var body = request.Body ?? current.Body;

        var check = await _sendMailValidator.ValidateAsync(new SendMailRequest(addresses, null, subject, body, null), cancellationToken);
        if (!check.IsValid)
        {
            throw new AssistantRequestException(string.Join(" ", check.Errors.Select(error => error.ErrorMessage).Distinct()));
        }

        var recipients = await ResolveRecipientsAsync(addresses, cancellationToken);
        action.PayloadJson = JsonSerializer.Serialize(new SendMailPayload(recipients, subject.Trim(), body, current.TaskId), Json);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToCard(action, now);
    }

    public async Task<AssistantPendingActionDto?> CancelAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default)
    {
        var action = await _dbContext.AssistantPendingActions
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == caller.UserId, cancellationToken);
        if (action is null)
        {
            return null;
        }

        var now = UtcNow;
        if (action.Status == AssistantActionStatus.Pending && action.ExpiresAt > now)
        {
            action.Status = AssistantActionStatus.Cancelled;
            action.ResolvedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "cancelled", cancellationToken);
        }

        return ToCard(action, now);
    }

    // ------------------------------------------------------------------------------------------- confirm

    public async Task<AssistantActionResultDto?> ConfirmAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default)
    {
        var action = await _dbContext.AssistantPendingActions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == caller.UserId, cancellationToken);
        if (action is null)
        {
            return null;
        }

        var now = UtcNow;
        if (action.Status == AssistantActionStatus.Pending && action.ExpiresAt <= now)
        {
            await MarkAsync(action.Id, AssistantActionStatus.Expired, now, cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "expired", cancellationToken);
            throw new AssistantActionConflictException("This request has expired. Please ask TFA again.");
        }

        if (action.Status != AssistantActionStatus.Pending)
        {
            throw new AssistantActionConflictException(action.Status == AssistantActionStatus.Confirmed
                ? "This action has already been done."
                : $"This action can no longer be confirmed ({action.Status.ToString().ToLowerInvariant()}). Please ask TFA again.");
        }

        // The permission is checked again now, with the caller's current token, not as it was when the action was prepared.
        var (policy, description, who) = AssistantActionRules.Requirement(action.Kind);
        if (!await _authorizer.IsAllowedAsync(caller.Principal, policy, cancellationToken))
        {
            await MarkAsync(action.Id, AssistantActionStatus.Refused, now, cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "refused", cancellationToken);
            var refusal = AssistantTexts.PermissionDenied(description, who);
            return new AssistantActionResultDto(
                action.Id,
                AssistantActionStatus.Refused,
                refusal,
                await StoreReplyAsync(caller.UserId, action, refusal, AssistantMessageKind.Refusal, cancellationToken));
        }

        // Claim it: only one of several simultaneous confirms can move Pending to Confirmed, so it runs at most once.
        var claimed = await _dbContext.AssistantPendingActions
            .Where(candidate => candidate.Id == action.Id && candidate.Status == AssistantActionStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(candidate => candidate.Status, AssistantActionStatus.Confirmed)
                    .SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken);
        if (claimed == 0)
        {
            throw new AssistantActionConflictException("This action has already been done.");
        }

        string message;
        var status = AssistantActionStatus.Confirmed;
        try
        {
            message = await ExecuteAsync(caller, action, cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "confirmed", cancellationToken);
        }
        catch (Exception exception) when (exception is MailValidationException or InvalidOperationException or ValidationException)
        {
            status = AssistantActionStatus.Failed;
            await MarkAsync(action.Id, AssistantActionStatus.Failed, now, cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "failed", cancellationToken);
            message = $"I could not do that: {exception.Message.TrimEnd('.')}. Nothing was {(action.Kind == AssistantActionKind.SendMail ? "sent" : "changed")}.";
        }

        return new AssistantActionResultDto(
            action.Id,
            status,
            message,
            await StoreReplyAsync(caller.UserId, action, message, AssistantMessageKind.Text, cancellationToken));
    }

    private async Task<string> ExecuteAsync(AssistantCaller caller, AssistantPendingAction action, CancellationToken cancellationToken)
    {
        switch (action.Kind)
        {
            case AssistantActionKind.SendMail:
                var mail = JsonSerializer.Deserialize<SendMailPayload>(action.PayloadJson, Json)!;
                var request = new SendMailRequest(mail.To.Select(to => to.Email).ToList(), null, mail.Subject, mail.Body, null);
                var validation = await _sendMailValidator.ValidateAsync(request, cancellationToken);
                if (!validation.IsValid)
                {
                    throw new ValidationException(validation.Errors);
                }

                var sent = await _mail.SendAsync(caller.UserId, request, cancellationToken)
                    ?? throw new InvalidOperationException("the mail could not be sent");
                return $"Done. Your mail \"{sent.Message.Subject}\" was sent to {string.Join(", ", mail.To.Select(to => to.FullName))}.";

            case AssistantActionKind.UpdateOwnProfile:
                var profile = JsonSerializer.Deserialize<UpdateProfilePayload>(action.PayloadJson, Json)!;
                var profileRequest = new UpdateOwnProfileRequest(profile.FullName, profile.Email);
                var profileValidation = await _profileValidator.ValidateAsync(profileRequest, cancellationToken);
                if (!profileValidation.IsValid)
                {
                    throw new ValidationException(profileValidation.Errors);
                }

                _ = await _users.UpdateOwnProfileAsync(caller.UserId, profileRequest, cancellationToken)
                    ?? throw new InvalidOperationException("your account was not found");
                return "Done. Your profile was updated.";

            default:
                var change = JsonSerializer.Deserialize<ChangeRolePayload>(action.PayloadJson, Json)!;
                var target = await _users.GetByIdAsync(change.TargetUserId, cancellationToken)
                    ?? throw new InvalidOperationException("that user was not found");

                // UpdateAsync replaces every field, so everything except the role is passed through unchanged.
                var updated = await _users.UpdateAsync(
                    target.Id,
                    new UpdateUserRequest(target.EmployeeCode, target.FullName, target.Username, target.Email, change.NewRole, target.DepartmentId),
                    cancellationToken) ?? throw new InvalidOperationException("that user was not found");
                return $"Done. {updated.FullName} is now a {updated.Role}. They were signed out and will use the new role the next time they sign in.";
        }
    }

    private async Task<AssistantMessageDto> StoreReplyAsync(
        int userId,
        AssistantPendingAction action,
        string text,
        AssistantMessageKind kind,
        CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var message = new AssistantMessage
        {
            ConversationId = action.ConversationId,
            Role = AssistantMessageRole.Assistant,
            Content = text,
            MetaJson = JsonSerializer.Serialize(new AssistantMessageMeta(kind, null, null, null), Json),
            CreatedAt = now
        };
        _dbContext.AssistantMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AssistantMessageDto(message.Id, "Assistant", text, kind, DateTime.SpecifyKind(now, DateTimeKind.Utc), null, [], [], null);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private Task MarkAsync(Guid id, AssistantActionStatus status, DateTime now, CancellationToken cancellationToken) =>
        _dbContext.AssistantPendingActions
            .Where(candidate => candidate.Id == id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(candidate => candidate.Status, status).SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken);

    private static void EnsurePending(AssistantPendingAction action, DateTime now)
    {
        if (action.Status != AssistantActionStatus.Pending)
        {
            throw new AssistantActionConflictException("This action can no longer be changed. Please ask TFA again.");
        }

        if (action.ExpiresAt <= now)
        {
            throw new AssistantActionConflictException("This request has expired. Please ask TFA again.");
        }
    }

    /// <summary>Resolves addresses to active TaskFlow users (the global filter already hides deleted users). An address
    /// that is not an active user stops the edit and is named, so a mail can never be addressed to an unknown person.</summary>
    internal async Task<IReadOnlyList<MailRecipientRef>> ResolveRecipientsAsync(IEnumerable<string> addresses, CancellationToken cancellationToken)
    {
        var wanted = MailRules.NormalizeAddresses(addresses.ToList());
        var found = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && wanted.Contains(user.Email.ToLower()))
            .Select(user => new MailRecipientRef(user.Id, user.FullName, user.Email))
            .ToListAsync(cancellationToken);

        var missing = wanted.Where(address => found.All(user => !string.Equals(user.Email, address, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count > 0)
        {
            throw new AssistantRequestException($"These are not active TaskFlow users: {string.Join(", ", missing)}.");
        }

        return wanted.Select(address => found.First(user => string.Equals(user.Email, address, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
