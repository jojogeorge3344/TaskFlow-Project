using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.IntegrationTests.TestSupport;

public class TaskFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"taskflow-tests-{Guid.NewGuid():N}.db");

    /// <summary>The storage root for this factory, so no test ever creates the real <c>C:\TaskFlow</c> folder.</summary>
    public string StorageRootPath { get; } =
        Path.Combine(Path.GetTempPath(), $"taskflow-root-tests-{Guid.NewGuid():N}");

    /// <summary>Attachment files for this factory's mail tests, removed on dispose.</summary>
    public string AttachmentsPath { get; } =
        Path.Combine(Path.GetTempPath(), $"taskflow-mail-tests-{Guid.NewGuid():N}");

    /// <summary>Data Protection key ring for this factory's AI provider keys, removed on dispose.</summary>
    public string KeyRingPath { get; } =
        Path.Combine(Path.GetTempPath(), $"taskflow-dp-tests-{Guid.NewGuid():N}");

    /// <summary>The AI providers used by the tests, scripted so nothing reaches a real model.</summary>
    public IReadOnlyDictionary<AiProviderKind, ScriptedChatProvider> Providers { get; } =
        Enum.GetValues<AiProviderKind>().ToDictionary(kind => kind, kind => new ScriptedChatProvider(kind));

    /// <summary>Everything the API logged, for asserting that secrets never reach the logs.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TaskFlow.Api.Integrations.Ai.Providers.IChatModelProvider>();
            foreach (var provider in Providers.Values)
            {
                services.AddSingleton<TaskFlow.Api.Integrations.Ai.Providers.IChatModelProvider>(provider);
            }
        });

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Root"] = StorageRootPath,
                ["Jwt:Issuer"] = "TaskFlow",
                ["Jwt:Key"] = new string('k', 64),
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "7",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Seed:Enabled"] = "false",
                // Tests drive the reminder engine directly; the background scheduler would add nondeterminism.
                ["Reminders:SchedulerEnabled"] = "false",
                ["Mail:AttachmentsPath"] = AttachmentsPath,
                ["AiProviders:KeyRingPath"] = KeyRingPath,
                // Most tests sign in without an authenticator app; the gate has its own tests that switch this on.
                ["Mfa:Required"] = "false"
            });
        });
    }

    public async System.Threading.Tasks.Task SeedUserAsync(
        string username,
        string email,
        string password,
        string role,
        bool isActive = true)
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync(user => user.Username == username))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = email,
            RoleId = RoleIdFor(role),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static int RoleIdFor(string roleName) => roleName switch
    {
        SystemRoles.Administrator => SystemRoles.AdministratorId,
        SystemRoles.Manager => SystemRoles.ManagerId,
        SystemRoles.Employee => SystemRoles.EmployeeId,
        _ => throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Not a system role.")
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(AttachmentsPath))
        {
            try
            {
                Directory.Delete(AttachmentsPath, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        if (disposing && Directory.Exists(KeyRingPath))
        {
            try
            {
                Directory.Delete(KeyRingPath, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        if (disposing && Directory.Exists(StorageRootPath))
        {
            try
            {
                Directory.Delete(StorageRootPath, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        if (disposing && File.Exists(_databasePath))
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
            }
        }
    }
}
