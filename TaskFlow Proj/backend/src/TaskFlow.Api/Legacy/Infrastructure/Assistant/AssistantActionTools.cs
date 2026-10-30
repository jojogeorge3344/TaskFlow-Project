using TaskFlow.Api.Dtos.Profile;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Tasks;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Assistant;

public record ResolvedUser(int Id, string FullName, string Email, string Role, bool IsActive);

/// <summary>Finds TaskFlow users for a name or an address. The model never supplies a user id: it names a person, and
/// the server decides who that is, or says it is ambiguous.</summary>
public class AssistantUserResolver
{
    private const int MaxCandidates = 5;

    private readonly AppDbContext _dbContext;

    public AssistantUserResolver(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public record Match(ResolvedUser? User, IReadOnlyList<ResolvedUser> Candidates);

    public record RoleRef(int Id, string Name);

    /// <summary>An existing role by name, or null. The model names a role; the server decides which role that is.</summary>
    public async Task<RoleRef?> FindRoleAsync(string? name, CancellationToken cancellationToken)
    {
        var term = name?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return null;
        }

        return await _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.Name == term)
            .Select(role => new RoleRef(role.Id, role.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>An exact address wins, then an exact full name, then a single partial match. More than one partial match
    /// returns the candidates so the user can be asked which one they mean.</summary>
    public async Task<Match> FindAsync(string query, CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length == 0)
        {
            return new Match(null, []);
        }

        var lower = term.ToLowerInvariant();
        var users = _dbContext.Users.AsNoTracking().Where(user => user.IsActive);

        if (term.Contains('@'))
        {
            var byEmail = await users
                .Where(user => user.Email.ToLower() == lower)
                .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
            return new Match(byEmail, byEmail is null ? [] : [byEmail]);
        }

        var exact = await users
            .Where(user => user.FullName.ToLower() == lower || user.Username.ToLower() == lower)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .Take(MaxCandidates + 1)
            .ToListAsync(cancellationToken);
        if (exact.Count == 1)
        {
            return new Match(exact[0], exact);
        }

        var escaped = lower.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var partial = await users
            .Where(user => EF.Functions.Like(user.FullName, $"%{escaped}%", "\\") || EF.Functions.Like(user.Username, $"%{escaped}%", "\\"))
            .OrderBy(user => user.FullName)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .Take(MaxCandidates)
            .ToListAsync(cancellationToken);

        return partial.Count == 1 ? new Match(partial[0], partial) : new Match(null, exact.Count > 1 ? exact.Take(MaxCandidates).ToList() : partial);
    }
}

/// <summary>"Who is my manager?" TaskFlow has no manager field, so the answer is the task's creator when that is a
/// different active user, otherwise the single active Manager in the sender's project. With none, or several, the
/// user is asked to choose.</summary>
public class AssistantManagerResolver
{
    private readonly AppDbContext _dbContext;
    private readonly ITaskService _tasks;

    public AssistantManagerResolver(AppDbContext dbContext, ITaskService tasks)
    {
        _dbContext = dbContext;
        _tasks = tasks;
    }

    public record Result(ResolvedUser? Manager, IReadOnlyList<ResolvedUser> Candidates, string? Problem);

    public async Task<Result> ResolveAsync(AssistantCaller caller, int? taskId, CancellationToken cancellationToken)
    {
        if (taskId is { } id)
        {
            var task = await _tasks.GetDetailsAsync(id, caller.UserId, caller.Role, cancellationToken);
            if (task is null)
            {
                return new Result(null, [], "That task was not found, or you do not have access to it.");
            }

            if (task.CreatedByUserId != caller.UserId)
            {
                var creator = await _dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == task.CreatedByUserId && user.IsActive)
                    .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
                    .FirstOrDefaultAsync(cancellationToken);
                if (creator is not null)
                {
                    return new Result(creator, [creator], null);
                }
            }
        }

        // A person's managers are the managers of the departments they belong to.
        var departmentIds = await _dbContext.UserDepartments
            .AsNoTracking()
            .Where(membership => membership.UserId == caller.UserId)
            .Select(membership => membership.DepartmentId)
            .ToListAsync(cancellationToken);
        if (departmentIds.Count == 0)
        {
            return new Result(null, [], "You are not in a department, so I cannot tell who your manager is.");
        }

        var managers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.RoleId == SystemRoles.ManagerId && user.IsActive && user.Id != caller.UserId
                && user.ManagedDepartments.Any(link => departmentIds.Contains(link.DepartmentId)))
            .OrderBy(user => user.FullName)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .ToListAsync(cancellationToken);

        return managers.Count switch
        {
            1 => new Result(managers[0], managers, null),
            0 => new Result(null, [], "I could not find a manager for you."),
            _ => new Result(null, managers, "There is more than one manager in your departments.")
        };
    }
}

public class DraftMailTool : IAssistantTool
{
    private readonly AssistantUserResolver _users;
    private readonly AssistantManagerResolver _managers;
    private readonly IAssistantActionService _actions;
    private readonly IValidator<SendMailRequest> _validator;

    public DraftMailTool(
        AssistantUserResolver users,
        AssistantManagerResolver managers,
        IAssistantActionService actions,
        IValidator<SendMailRequest> validator)
    {
        _users = users;
        _managers = managers;
        _actions = actions;
        _validator = validator;
    }

    public string Name => "draft_mail";

    public string Description =>
        "Prepares a TaskFlow mail for the user to review. Nothing is sent: the user must confirm. Use recipient=\"manager\" for 'my manager' (give taskId when the mail is about a task). Otherwise list the people in 'to' by name or email.";

    public string ParametersSchema => """
        {"type":"object","properties":{
          "recipient":{"type":"string","enum":["manager"],"description":"Use for 'my manager'."},
          "to":{"type":"array","items":{"type":"string"},"description":"Names or email addresses of TaskFlow users."},
          "taskId":{"type":"integer","description":"The task the mail is about, if any."},
          "subject":{"type":"string"},
          "body":{"type":"string","description":"The message, written in the user's voice."}},
         "required":["subject","body"]}
        """;

    public string? Policy => null;
    public string ActionDescription => "Sending mail";
    public string WhoCanDoIt => "a signed-in user";
    public AssistantActionKind? ActionKind => AssistantActionKind.SendMail;

    public async Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var subject = ToolJson.String(arguments, "subject")?.Trim() ?? string.Empty;
        var body = ToolJson.String(arguments, "body")?.Trim() ?? string.Empty;
        var taskId = ToolJson.Int(arguments, "taskId");

        var recipients = new List<ResolvedUser>();
        if (ToolJson.String(arguments, "recipient") == "manager")
        {
            var resolved = await _managers.ResolveAsync(caller, taskId, cancellationToken);
            if (resolved.Manager is null)
            {
                return AskTheUser(resolved.Problem ?? "I could not tell who the manager is.", resolved.Candidates);
            }

            recipients.Add(resolved.Manager);
        }

        if (arguments.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in to.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
            {
                var name = entry.GetString() ?? string.Empty;
                var match = await _users.FindAsync(name, cancellationToken);
                if (match.User is null)
                {
                    return AskTheUser(
                        match.Candidates.Count == 0
                            ? $"I could not find an active TaskFlow user called \"{name}\"."
                            : $"More than one person matches \"{name}\".",
                        match.Candidates);
                }

                recipients.Add(match.User);
            }
        }

        recipients = recipients.GroupBy(user => user.Id).Select(group => group.First()).ToList();
        if (recipients.Count == 0)
        {
            return new ToolOutcome("There is no recipient. Ask the user who the mail is for.");
        }

        var check = await _validator.ValidateAsync(
            new SendMailRequest(recipients.Select(user => user.Email).ToList(), null, subject, body, null), cancellationToken);
        if (!check.IsValid)
        {
            return new ToolOutcome("The draft is not valid: " + string.Join(" ", check.Errors.Select(error => error.ErrorMessage).Distinct()));
        }

        var card = await _actions.CreateAsync(
            caller,
            conversationId,
            AssistantActionKind.SendMail,
            new SendMailPayload(recipients.Select(user => new MailRecipientRef(user.Id, user.FullName, user.Email)).ToList(), subject, body, taskId),
            cancellationToken);

        return new ToolOutcome(
            "The draft is now shown to the user for review. It has not been sent.",
            AssistantTexts.ActionPrepared,
            Links: taskId is { } id ? [new AssistantLinkDto("Open the task", $"/tasks/{id}")] : null,
            PendingActionId: card.Id);
    }

    private static ToolOutcome AskTheUser(string problem, IReadOnlyList<ResolvedUser> candidates) =>
        new(candidates.Count > 0
            ? $"{problem} Ask the user which one: {string.Join("; ", candidates.Select(user => $"{user.FullName} <{user.Email}>"))}."
            : $"{problem} Ask the user who the mail should go to.");
}

public class ProposeProfileUpdateTool : IAssistantTool
{
    private readonly AppDbContext _dbContext;
    private readonly IAssistantActionService _actions;
    private readonly IValidator<UpdateOwnProfileRequest> _validator;

    public ProposeProfileUpdateTool(AppDbContext dbContext, IAssistantActionService actions, IValidator<UpdateOwnProfileRequest> validator)
    {
        _dbContext = dbContext;
        _actions = actions;
        _validator = validator;
    }

    public string Name => "propose_profile_update";

    public string Description =>
        "Prepares a change to the user's own name or email for them to confirm. It cannot change anything else. For passwords use explain_password_change.";

    public string ParametersSchema => """
        {"type":"object","properties":{"fullName":{"type":"string"},"email":{"type":"string"}}}
        """;

    public string? Policy => null;
    public string ActionDescription => "Updating a profile";
    public string WhoCanDoIt => "the account owner";
    public AssistantActionKind? ActionKind => AssistantActionKind.UpdateOwnProfile;

    public async Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var newName = ToolJson.String(arguments, "fullName")?.Trim();
        var newEmail = ToolJson.String(arguments, "email")?.Trim();
        if (string.IsNullOrEmpty(newName) && string.IsNullOrEmpty(newEmail))
        {
            return new ToolOutcome("Ask the user what their new name or email should be.");
        }

        var me = await _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == caller.UserId)
            .Select(user => new { user.FullName, user.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (me is null)
        {
            return new ToolOutcome("The user's account was not found.");
        }

        var fullName = string.IsNullOrEmpty(newName) ? me.FullName : newName;
        var email = string.IsNullOrEmpty(newEmail) ? me.Email : newEmail;

        var check = await _validator.ValidateAsync(new UpdateOwnProfileRequest(fullName, email), cancellationToken);
        if (!check.IsValid)
        {
            return new ToolOutcome("That change is not valid: " + string.Join(" ", check.Errors.Select(error => error.ErrorMessage).Distinct()));
        }

        var lowered = email.ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(user => user.Id != caller.UserId && user.Email.ToLower() == lowered, cancellationToken))
        {
            return new ToolOutcome("That email address is already used by another account. Ask the user for a different one.");
        }

        var card = await _actions.CreateAsync(
            caller,
            conversationId,
            AssistantActionKind.UpdateOwnProfile,
            new UpdateProfilePayload(fullName, email, me.FullName, me.Email),
            cancellationToken);

        return new ToolOutcome(
            "The change is now shown to the user for confirmation. Nothing has changed yet.",
            AssistantTexts.ActionPrepared,
            Links: [new AssistantLinkDto("My Profile", "/profile")],
            PendingActionId: card.Id);
    }
}

/// <summary>Changing a role needs the <c>roles.manage</c> permission, checked here when the tool is used and again when the
/// action is confirmed. A non-administrator never reaches <see cref="ExecuteAsync"/>: the gate answers with the fixed
/// refusal.</summary>
public class ProposeRoleChangeTool : IAssistantTool
{
    private readonly AssistantUserResolver _users;
    private readonly IAssistantActionService _actions;

    public ProposeRoleChangeTool(AssistantUserResolver users, IAssistantActionService actions)
    {
        _users = users;
        _actions = actions;
    }

    public string Name => "propose_role_change";

    public string Description =>
        "Prepares a change of another user's role to an existing role, such as Administrator, Manager, Employee or a custom role, for the administrator to confirm. Only administrators can use this.";

    public string ParametersSchema => """
        {"type":"object","properties":{
          "user":{"type":"string","description":"The person's name or email."},
          "role":{"type":"string","description":"The name of an existing role."}},
         "required":["user","role"]}
        """;

    public string? Policy => AssistantPolicies.RolesManage;
    public string ActionDescription => "Changing a user's role";
    public string WhoCanDoIt => "an authorized administrator";
    public AssistantActionKind? ActionKind => AssistantActionKind.ChangeUserRole;

    public async Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var name = ToolJson.String(arguments, "user") ?? string.Empty;
        var newRole = await _users.FindRoleAsync(ToolJson.String(arguments, "role"), cancellationToken);
        if (newRole is null)
        {
            return new ToolOutcome("That role does not exist. Ask the user which existing role they mean.");
        }

        var match = await _users.FindAsync(name, cancellationToken);
        if (match.User is not { } target)
        {
            return new ToolOutcome(match.Candidates.Count == 0
                ? $"I could not find an active TaskFlow user called \"{name}\"."
                : $"More than one person matches \"{name}\": {string.Join("; ", match.Candidates.Select(user => $"{user.FullName} <{user.Email}>"))}. Ask which one.");
        }

        // Changing your own role could leave the system without an administrator, so TFA leaves that to a colleague.
        if (target.Id == caller.UserId)
        {
            const string own = "For safety I do not change your own role. Please ask another administrator to do it.";
            return new ToolOutcome(own, own);
        }

        if (string.Equals(target.Role, newRole.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolOutcome($"{target.FullName} is already a {newRole.Name}. Tell the user nothing needs to change.");
        }

        var card = await _actions.CreateAsync(
            caller,
            conversationId,
            AssistantActionKind.ChangeUserRole,
            new ChangeRolePayload(target.Id, target.FullName, target.Role, newRole.Id, newRole.Name),
            cancellationToken);

        return new ToolOutcome(
            "The role change is now shown to the administrator for confirmation. Nothing has changed yet.",
            AssistantTexts.ActionPrepared,
            Links: [new AssistantLinkDto("Employee Management", "/employees")],
            PendingActionId: card.Id);
    }
}
