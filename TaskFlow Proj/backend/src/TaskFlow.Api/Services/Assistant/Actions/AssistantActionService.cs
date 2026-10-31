using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Common.Exceptions;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Repositories.Assistant.Messages;
using TaskFlow.Api.Repositories.Assistant.PendingActions;
using TaskFlow.Api.Repositories.Assistant.People;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Services.Assistant.Access;
using TaskFlow.Api.Services.Assistant.AuditLog;
using TaskFlow.Api.Services.Assistant.Chat;
using TaskFlow.Api.Services.Communication.Mail.Messages;
using TaskFlow.Api.Services.Management.EmployeeManagement.Employees;
using TaskFlow.Api.Authorization.Assistant;

namespace TaskFlow.Api.Services.Assistant.Actions;

/// <summary>Pending actions and their confirmation. Nothing here is reachable by the model: it can only ask for an
/// action to be created, and an action only ever runs from <see cref="ConfirmAsync"/>, called by the user's own
/// request. The permission check runs again at that moment, with the user's current token.</summary>
public class AssistantActionService : IAssistantActionService
{
    private static readonly JsonSerializerOptions Json = AssistantService.MetaJson;

    private readonly IAssistantPendingActionRepository _actions;
    private readonly IAssistantMessageRepository _messages;
    private readonly IAssistantDirectoryRepository _directory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMailService _mail;
    private readonly IEmployeeService _users;
    private readonly TaskFlow.Api.Services.Profile.IProfileService _profile;
    private readonly IAssistantAuthorizer _authorizer;
    private readonly IAssistantAuditLog _audit;
    private readonly IValidator<SendMailRequest> _sendMailValidator;
    private readonly IValidator<UpdateOwnProfileRequest> _profileValidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssistantActionService>? _logger;

    public AssistantActionService(
        IAssistantPendingActionRepository actions,
        IAssistantMessageRepository messages,
        IAssistantDirectoryRepository directory,
        IUnitOfWork unitOfWork,
        IMailService mail,
        IEmployeeService users,
        TaskFlow.Api.Services.Profile.IProfileService profile,
        IAssistantAuthorizer authorizer,
        IAssistantAuditLog audit,
        IValidator<SendMailRequest> sendMailValidator,
        IValidator<UpdateOwnProfileRequest> profileValidator,
        TimeProvider timeProvider,
        ILogger<AssistantActionService>? logger = null)
    {
        _logger = logger;
        _actions = actions;
        _messages = messages;
        _directory = directory;
        _unitOfWork = unitOfWork;
        _mail = mail;
        _users = users;
        _profile = profile;
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
        _actions.Add(action);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        var actions = await _actions.GetForUserAsync(userId, actionIds, cancellationToken);
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
        var action = await _actions.FindForUserForUpdateAsync(id, caller.UserId, cancellationToken);
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToCard(action, now);
    }

    public async Task<AssistantPendingActionDto?> CancelAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default)
    {
        var action = await _actions.FindForUserForUpdateAsync(id, caller.UserId, cancellationToken);
        if (action is null)
        {
            return null;
        }

        var now = UtcNow;
        if (action.Status == AssistantActionStatus.Pending && action.ExpiresAt > now)
        {
            action.Status = AssistantActionStatus.Cancelled;
            action.ResolvedAt = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync(caller.UserId, action.Kind, "cancelled", cancellationToken);
        }

        return ToCard(action, now);
    }

    // ------------------------------------------------------------------------------------------- confirm

    public async Task<AssistantActionResultDto?> ConfirmAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default)
    {
        var action = await _actions.FindForUserAsync(id, caller.UserId, cancellationToken);
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
        if (!await _actions.TryClaimAsync(action.Id, now, cancellationToken))
        {
            throw new AssistantActionConflictException("This action has already been done.");
        }

        string message;
        var status = AssistantActionStatus.Confirmed;
        try
        {
            message = await ExecuteAsync(caller, action, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The action was claimed before it ran, so any failure, expected or not, must leave it Failed and not
            // Confirmed. Application rule failures say why; anything else is logged and reported in general terms.
            // A clash in the database (a concurrent edit, a duplicate value) is not a rule the user broke, so it is logged
            // and reported in general terms like any other server failure.
            var expected = exception is (AppException and not (ConcurrencyConflictException or UniqueConstraintException))
                or ValidationException;
            if (!expected)
            {
                _logger?.LogError(exception, "TFA action {ActionKind} failed unexpectedly", action.Kind);
            }

            status = AssistantActionStatus.Failed;
            await MarkAsync(action.Id, AssistantActionStatus.Failed, now, CancellationToken.None);
            try
            {
                await _audit.RecordAsync(caller.UserId, action.Kind, "failed", CancellationToken.None);
            }
            catch (Exception auditException) when (auditException is not OperationCanceledException)
            {
                // The action is already marked Failed. A failing audit store must not hide the result from the user.
                _logger?.LogError(auditException, "TFA action {ActionKind} failed and its audit entry could not be written", action.Kind);
            }

            var reason = expected ? exception.Message.TrimEnd('.') : "something went wrong on the server";
            message = $"I could not do that: {reason}. Nothing was {(action.Kind == AssistantActionKind.SendMail ? "sent" : "changed")}.";
        }

        // The action has finished. Recording it and storing the reply must never turn a success into a reported failure:
        // a mail that went out would then be called "not sent" and the user might send it again.
        if (status == AssistantActionStatus.Confirmed)
        {
            try
            {
                await _audit.RecordAsync(caller.UserId, action.Kind, "confirmed", CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger?.LogError(exception, "TFA action {ActionKind} was done but its audit entry could not be written", action.Kind);
            }
        }

        AssistantMessageDto? reply = null;
        try
        {
            reply = await StoreReplyAsync(caller.UserId, action, message, AssistantMessageKind.Text, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger?.LogError(exception, "TFA action {ActionKind} finished but its result message could not be stored", action.Kind);
        }

        return new AssistantActionResultDto(action.Id, status, message, reply);
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
                    ?? throw new BusinessRuleException("the mail could not be sent");
                return $"Done. Your mail \"{sent.Message.Subject}\" was sent to {string.Join(", ", mail.To.Select(to => to.FullName))}.";

            case AssistantActionKind.UpdateOwnProfile:
                var profile = JsonSerializer.Deserialize<UpdateProfilePayload>(action.PayloadJson, Json)!;
                var profileRequest = new UpdateOwnProfileRequest(profile.FullName, profile.Email);
                var profileValidation = await _profileValidator.ValidateAsync(profileRequest, cancellationToken);
                if (!profileValidation.IsValid)
                {
                    throw new ValidationException(profileValidation.Errors);
                }

                _ = await _profile.UpdateAsync(caller.UserId, profileRequest, cancellationToken)
                    ?? throw new BusinessRuleException("your account was not found");
                return "Done. Your profile was updated.";

            default:
                var change = JsonSerializer.Deserialize<ChangeRolePayload>(action.PayloadJson, Json)!;
                var target = await _users.GetByIdAsync(change.TargetUserId, cancellationToken)
                    ?? throw new BusinessRuleException("that user was not found");

                // UpdateAsync replaces every field, so everything except the role is passed through unchanged.
                var updated = await _users.UpdateAsync(
                    target.Id,
                    new UpdateUserRequest(target.EmployeeCode, target.FullName, target.Username, target.Email, change.NewRoleId, target.ProjectAllocations.Select(a => new ProjectAllocationInput(a.ProjectId, a.PositionId, a.Percent)).ToList(), target.Departments.Select(d => d.Id).ToList()),
                    cancellationToken) ?? throw new BusinessRuleException("that user was not found");
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
        _messages.Add(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssistantMessageDto(message.Id, "Assistant", text, kind, DateTime.SpecifyKind(now, DateTimeKind.Utc), null, [], [], null, []);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private Task MarkAsync(Guid id, AssistantActionStatus status, DateTime now, CancellationToken cancellationToken) =>
        _actions.SetStatusAsync(id, status, now, cancellationToken);

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
        var found = await _directory.GetActiveRecipientsAsync(wanted, cancellationToken);

        var missing = wanted.Where(address => found.All(user => !string.Equals(user.Email, address, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count > 0)
        {
            throw new AssistantRequestException($"These are not active TaskFlow users: {string.Join(", ", missing)}.");
        }

        return wanted.Select(address => found.First(user => string.Equals(user.Email, address, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
