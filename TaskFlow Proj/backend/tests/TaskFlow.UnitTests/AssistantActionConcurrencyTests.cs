using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile.Validators;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Common.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Assistant;
using TaskFlow.Application.Mail.Validators;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Assistant;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;
using User = TaskFlow.Api.Models.Entities.User;

namespace TaskFlow.UnitTests;

/// <summary>Two requests confirming the same action at the same moment, each with its own database connection, must
/// still send the mail once.</summary>
public class AssistantActionConcurrencyTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-concurrency-{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return Task.CompletedTask;
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private AssistantActionService NewService(AppDbContext db)
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 19, 9, 0, 0));
        var mail = new MailService(db, clock, new AppClock(new ApplicationSettingsRepository(db), clock, Options.Create(new ApplicationOptions { Timezone = "UTC" }), NullLogger<AppClock>.Instance));
        return new AssistantActionService(
            db,
            mail,
            TestServices.Employees(db, new PasswordHasher<User>()),
            new RolePolicyAuthorizer(),
            new AssistantAuditLog(db, clock),
            new SendMailRequestValidator(),
            new UpdateOwnProfileRequestValidator(),
            clock);
    }

    [Fact]
    public async Task TwoSimultaneousConfirmsSendTheMailOnce()
    {
        await using (var setup = NewContext())
        {
            await setup.Database.EnsureCreatedAsync();
            await TestAccessControl.SeedAsync(setup);
            setup.Users.AddRange(
                new User { FullName = "Asha", Username = "asha", Email = "asha@taskflow.com", RoleId = SystemRoles.EmployeeId, IsActive = true, PasswordHash = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new User { FullName = "Ravi", Username = "ravi", Email = "ravi@taskflow.com", RoleId = SystemRoles.ManagerId, IsActive = true, PasswordHash = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        int ashaId;
        int raviId;
        Guid actionId;
        await using (var prepare = NewContext())
        {
            ashaId = (await prepare.Users.SingleAsync(user => user.Username == "asha")).Id;
            raviId = (await prepare.Users.SingleAsync(user => user.Username == "ravi")).Id;
            var conversation = new TaskFlow.Api.Models.Entities.AssistantConversation { UserId = ashaId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            prepare.AssistantConversations.Add(conversation);
            await prepare.SaveChangesAsync();
            var card = await NewService(prepare).CreateAsync(
                AssistantServiceHarness.Caller(new User { Id = ashaId, RoleId = SystemRoles.EmployeeId }),
                conversation.Id,
                AssistantActionKind.SendMail,
                new SendMailPayload([new MailRecipientRef(raviId, "Ravi", "ravi@taskflow.com")], "Hello", "Body", null));
            actionId = card.Id;
        }

        var caller = AssistantServiceHarness.Caller(new User { Id = ashaId, RoleId = SystemRoles.EmployeeId });
        await using var first = NewContext();
        await using var second = NewContext();

        var results = await Task.WhenAll(
            Attempt(NewService(first), caller, actionId),
            Attempt(NewService(second), caller, actionId));

        Assert.Equal(1, results.Count(done => done));
        await using var check = NewContext();
        Assert.Equal(1, await check.MailMessages.CountAsync());
        Assert.Equal(1, await check.MailboxItems.CountAsync(item => item.OwnerUserId == raviId && item.Folder == MailFolder.Inbox));
    }

    private static async Task<bool> Attempt(AssistantActionService service, AssistantCaller caller, Guid id)
    {
        try
        {
            var result = await service.ConfirmAsync(caller, id);
            return result is { Status: AssistantActionStatus.Confirmed };
        }
        catch (AssistantActionConflictException)
        {
            return false;
        }
    }
}
