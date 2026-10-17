using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.IntegrationTests;

public class TaskFlowApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The SMTP password the API sees; tests assert it never appears in a response.</summary>
    public const string SmtpPassword = "integration-smtp-secret-7731";

    /// <summary>Replaces the MailKit sender so no test opens a socket.</summary>
    public FakeEmailSender EmailSender { get; } = new();

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"taskflow-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TaskFlow",
                ["Jwt:Key"] = new string('k', 64),
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "7",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Seed:Enabled"] = "false",
                // Tests drive the reminder engine directly; the background scheduler would add nondeterminism.
                ["Reminders:SchedulerEnabled"] = "false",
                ["Email:SmtpPassword"] = SmtpPassword
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    public async System.Threading.Tasks.Task SeedUserAsync(
        string username,
        string email,
        string password,
        Role role,
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
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

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

/// <summary>Records sent emails and returns <see cref="NextResult"/> (success by default).</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public EmailSendResult NextResult { get; set; } = EmailSendResult.Sent;

    public IReadOnlyList<EmailMessage> Sent { get { lock (_sent) { return _sent.ToList(); } } }

    public Task<EmailSendResult> SendAsync(EmailMessage message, SmtpSettings settings, CancellationToken cancellationToken = default)
    {
        lock (_sent)
        {
            _sent.Add(message);
        }

        return System.Threading.Tasks.Task.FromResult(NextResult);
    }
}
