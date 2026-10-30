using TaskFlow.Api.Services.Communication.Mail;
using TaskFlow.Api.Dtos.Communication.Mail.Validators;
using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile.Validators;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using System.Security.Claims;
using TaskFlow.Api.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Assistant;
using TaskFlow.Api.Persistence;
using User = TaskFlow.Api.Models.Entities.User;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

/// <summary>Real mail, user and task services over an in-memory database, with the real action service and tools. The
/// authorizer mirrors the API's policies by role, and time is controllable.</summary>
internal sealed class AssistantActionHarness : IAsyncDisposable
{
    public AssistantActionHarness()
    {
        Tracking = TaskTrackingServiceHarness.Create();
        Clock = new FixedTimeProvider(new DateTime(2026, 10, 19, 9, 0, 0));

        Engineering = new Project { Name = "Engineering", CreatedAt = DateTime.UtcNow };
        Db.Projects.Add(Engineering);
        EngineeringTeam = new Department { Name = "Engineering team", CreatedAt = DateTime.UtcNow };
        Db.Departments.Add(EngineeringTeam);
        Db.SaveChanges();

        Admin = AddUser("admin", SystemRoles.Administrator);
        Ravi = AddUser("ravi", SystemRoles.Manager, Engineering.Id);
        Asha = AddUser("asha", SystemRoles.Employee, Engineering.Id);
        Priya = AddUser("priya", SystemRoles.Employee, Engineering.Id);
    }

    public TaskTrackingServiceHarness Tracking { get; }
    public AppDbContext Db => Tracking.Db;
    public FixedTimeProvider Clock { get; }
    public RolePolicyAuthorizer Authorizer { get; } = new();
    public Project Engineering { get; }

    /// <summary>The department of everyone on the Engineering project: its managers manage it and the rest belong to it, which is how
    /// a person's manager is found now.</summary>
    public Department EngineeringTeam { get; }
    public User Admin { get; }
    public User Ravi { get; }
    public User Asha { get; }
    public User Priya { get; }

    public User AddUser(string username, string role, int? projectId = null, bool isActive = true)
    {
        var user = Tracking.AddUser(username, role, isActive);
        TestMemberships.Allocate(Db, user, projectId);
        if (projectId == Engineering.Id)
        {
            if (role == SystemRoles.Manager)
            {
                Db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = user.Id, DepartmentId = EngineeringTeam.Id });
                Db.SaveChanges();
            }
            else
            {
                TestMemberships.JoinDepartment(Db, user, EngineeringTeam.Id);
            }
        }
        return user;
    }

    public EmployeeService Users => TestServices.Employees(Db, new PasswordHasher<User>());

    public MailService Mail => TestServices.Mail(Db, Clock, new AppClock(new ApplicationSettingsRepository(Db), Clock, Options.Create(new ApplicationOptions { Timezone = "UTC" }), NullLogger<AppClock>.Instance));

    public AssistantAuditLog Audit => new(Db, Clock);

    public AssistantActionService Actions =>
        new(Db, Mail, Users, Authorizer, Audit, new SendMailRequestValidator(), new UpdateOwnProfileRequestValidator(), Clock);

    public AssistantUserResolver Resolver => new(Db);

    public AssistantManagerResolver Managers => new(Db, Tracking.TaskService);

    public AssistantConversation Conversation(User user)
    {
        var conversation = new AssistantConversation { UserId = user.Id, CreatedAt = Clock.GetUtcNow().UtcDateTime, UpdatedAt = Clock.GetUtcNow().UtcDateTime };
        Db.AssistantConversations.Add(conversation);
        Db.SaveChanges();
        return conversation;
    }

    public static AssistantCaller Caller(User user, string? roleOverride = null) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, roleOverride ?? TestAccessControl.NameFor(user.RoleId))
            ],
            "test")));

    public Task<AssistantPendingActionDto> PrepareMailAsync(User from, params User[] to) =>
        Actions.CreateAsync(
            Caller(from),
            Conversation(from).Id,
            AssistantActionKind.SendMail,
            new SendMailPayload(to.Select(user => new MailRecipientRef(user.Id, user.FullName, user.Email)).ToList(), "Update on TF-102", "It is in review.", null));

    public IQueryable<AssistantAudit> AuditFor(User user) => Db.AssistantAudits.AsNoTracking().Where(audit => audit.UserId == user.Id);

    public async Task<IReadOnlyList<string>> OutcomesAsync(User user) =>
        await AuditFor(user).OrderBy(audit => audit.Id).Select(audit => audit.Outcome).ToListAsync();

    public ValueTask DisposeAsync() => Tracking.DisposeAsync();
}

internal sealed class RecordingAudit : IAssistantAuditLog
{
    public List<(int UserId, AssistantActionKind Kind, string Outcome)> Entries { get; } = [];

    public Task RecordAsync(int userId, AssistantActionKind kind, string outcome, CancellationToken cancellationToken = default)
    {
        Entries.Add((userId, kind, outcome));
        return Task.CompletedTask;
    }
}
