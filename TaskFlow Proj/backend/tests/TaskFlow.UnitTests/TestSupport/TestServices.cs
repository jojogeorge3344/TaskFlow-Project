using TaskFlow.Api.Repositories.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement.Discussion;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Repositories.Communication.Notification.Reminders;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Services.Communication.Notification.Reminders;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Integrations.Storage;
using TaskFlow.Api.Repositories.Communication.Mail.Attachments;
using TaskFlow.Api.Services.Communication.Mail.Attachments;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Management.DepartmentManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;
using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Repositories.Auth.Accounts;
using TaskFlow.Api.Repositories.Auth.RefreshTokens;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Communication.Mail.Messages;
using TaskFlow.Api.Repositories.Communication.Notification.Notifications;
using TaskFlow.Api.Repositories.Dashboard.Overview;
using TaskFlow.Api.Repositories.Management.EmployeeManagement.Employees;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Projects;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;
using TaskFlow.Api.Repositories.Settings.RoleConfiguration.Roles;
using TaskFlow.Api.Services.Communication.Mail.Messages;
using TaskFlow.Api.Services.Communication.Notification.Notifications;
using TaskFlow.Api.Services.Communication.Notification.Publishing;
using TaskFlow.Api.Services.Dashboard.Analytics.Admin;
using TaskFlow.Api.Services.Dashboard.Analytics.Employee;
using TaskFlow.Api.Services.Dashboard.Analytics.Manager;
using TaskFlow.Api.Services.Dashboard.Analytics.Scopes;
using TaskFlow.Api.Services.Dashboard.Overview;
using TaskFlow.Api.Services.Management.EmployeeManagement.Employees;
using TaskFlow.Api.Services.Management.ProjectManagement.Projects;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Tasks;
using TaskFlow.Api.Services.Settings.ApplicationSettings.Clock;

namespace TaskFlow.UnitTests.TestSupport;

/// <summary>Builds services the way the API does, with real repositories over the test's database, so tests exercise the
/// same queries the API runs.</summary>
internal static class TestServices
{
    public static UserAllocationService Allocations(AppDbContext db) =>
        new(
            new UserAllocationRepository(db),
            new EmployeeRepository(db),
            new ProjectRepository(db),
            new PositionRepository(db),
            new DepartmentRepository(db),
            new UnitOfWork(db));

    public static EmployeeService Employees(AppDbContext db, IPasswordHasher<User> hasher, IUserAllocationService? allocations = null) =>
        new(
            new EmployeeRepository(db),
            new RoleRepository(db),
            new RefreshTokenRepository(db),
            new TaskRepository(db),
            new UnitOfWork(db),
            hasher,
            allocations ?? Allocations(db));

    public static MailService Mail(AppDbContext db, TimeProvider timeProvider, IAppClock clock) =>
        new(new MailRepository(db), new MailAttachmentRepository(db), new UnitOfWork(db), timeProvider, clock);

    public static MailAttachmentService MailAttachments(AppDbContext db, IMailFileStore store, TimeProvider timeProvider) =>
        new(new MailAttachmentRepository(db), new UnitOfWork(db), store, timeProvider);

    public static NotificationPublisher NotificationPublisher(AppDbContext db, TimeProvider timeProvider) =>
        new(new NotificationRepository(db), new ApplicationSettingsRepository(db), new UnitOfWork(db), timeProvider);

    public static NotificationService Notifications(AppDbContext db) => new(new NotificationRepository(db), new UnitOfWork(db));

    public static ReminderService Reminders(
        AppDbContext db,
        IAppClock clock,
        IReminderSettingsService settings,
        IOptions<MailOptions> mailOptions,
        ILogger<ReminderService> logger) =>
        new(
            new ReminderRepository(db),
            new NotificationRepository(db),
            new MailRepository(db),
            new EmployeeRepository(db),
            new UnitOfWork(db),
            clock,
            settings,
            mailOptions,
            logger);

    public static TaskService Tasks(
        AppDbContext db,
        ITaskActivityService activity,
        INotificationPublisher publisher,
        TimeProvider timeProvider,
        IReminderSettingsService reminderSettings,
        IAppClock clock,
        IMailService mail,
        ILogger<TaskService> logger) =>
        new(
            new TaskRepository(db),
            new EmployeeRepository(db),
            new ProjectRepository(db),
            new RoleRepository(db),
            new UnitOfWork(db),
            activity,
            publisher,
            timeProvider,
            reminderSettings,
            clock,
            mail,
            logger);

    public static TaskActivityService TaskActivity(AppDbContext db, TimeProvider timeProvider) =>
        new(new TaskActivityRepository(db), new TaskRepository(db), new UnitOfWork(db), timeProvider);

    public static TaskCommentService TaskComments(
        AppDbContext db,
        ITaskActivityService activity,
        INotificationPublisher publisher,
        TimeProvider timeProvider) =>
        new(
            new TaskCommentRepository(db),
            new TaskRepository(db),
            new EmployeeRepository(db),
            new MailRepository(db),
            new ApplicationSettingsRepository(db),
            new UnitOfWork(db),
            activity,
            publisher,
            timeProvider);

    public static ProjectService Projects(AppDbContext db, IUserAllocationService? allocations = null) =>
        new(new ProjectRepository(db), allocations ?? Allocations(db), new UnitOfWork(db));

    public static AdminDashboardScope AdminScope(AppDbContext db, IAppClock clock) =>
        new(new AnalyticsRepository(db), clock);

    public static ManagerDashboardScope ManagerScope(AppDbContext db, IAppClock clock) =>
        new(new AnalyticsRepository(db), new EmployeeRepository(db), clock);

    public static AdminAnalyticsService AdminAnalytics(AppDbContext db, IDashboardScope scope, IAppClock clock) =>
        new(new AnalyticsRepository(db), scope, clock);

    public static ManagerAnalyticsService ManagerAnalytics(AppDbContext db, IManagerDashboardScope scope, IAppClock clock) =>
        new(new AnalyticsRepository(db), scope, clock);

    public static EmployeeAnalyticsService EmployeeAnalytics(AppDbContext db, IAppClock clock) =>
        new(new AnalyticsRepository(db), new EmployeeDashboardScope(), clock);

    public static DashboardService Dashboard(AppDbContext db, IAppClock clock) =>
        new(new DashboardRepository(db), clock, ManagerScope(db, clock));

    public static TaskFlow.Api.Services.Profile.ProfileService Profile(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TaskFlow.Api.Services.Auth.Mfa.IMfaService? mfa = null) =>
        new(
            new EmployeeRepository(db),
            new RefreshTokenRepository(db),
            new UnitOfWork(db),
            hasher,
            mfa ?? new AcceptingMfaService(),
            new TaskFlow.Api.Services.Auth.AttemptLimits.AttemptLimiter(TimeProvider.System));

    public static TaskFlow.Api.Services.Auth.Mfa.MfaService Mfa(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TaskFlow.Api.Integrations.Security.MfaSecrets.IMfaSecretProtector protector,
        INotificationPublisher publisher,
        IOptions<TaskFlow.Api.Config.MfaOptions> options,
        TimeProvider timeProvider) =>
        new(
            new TaskFlow.Api.Repositories.Auth.Accounts.AccountRepository(db),
            new TaskFlow.Api.Repositories.Auth.Mfa.MfaBackupCodeRepository(db),
            new RefreshTokenRepository(db),
            new UnitOfWork(db),
            hasher,
            protector,
            publisher,
            options,
            timeProvider);

    public static TaskFlow.Api.Services.Auth.Login.AuthService Auth(
        AppDbContext db,
        TaskFlow.Api.Integrations.Security.Tokens.ITokenService tokenService,
        IPasswordHasher<User> hasher,
        TimeProvider timeProvider,
        TaskFlow.Api.Services.Auth.Mfa.IMfaService mfaService,
        TaskFlow.Api.Integrations.Security.MfaSecrets.IMfaSecretProtector protector,
        IOptions<TaskFlow.Api.Config.MfaOptions> options) =>
        new(
            new TaskFlow.Api.Repositories.Auth.Accounts.AccountRepository(db),
            new RefreshTokenRepository(db),
            new UnitOfWork(db),
            tokenService,
            hasher,
            timeProvider,
            mfaService,
            protector,
            options);

    public static TaskFlow.Api.Services.Auth.PasswordRecovery.PasswordRecoveryService PasswordRecovery(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        INotificationPublisher publisher,
        TimeProvider timeProvider)
    {
        var protector = new TaskFlow.Api.Integrations.Security.MfaSecrets.MfaSecretProtector(
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(),
            timeProvider);
        return new(
            new TaskFlow.Api.Repositories.Auth.Accounts.AccountRepository(db),
            new TaskFlow.Api.Repositories.Auth.PasswordRecovery.PasswordResetGrantRepository(db),
            new RefreshTokenRepository(db),
            new UnitOfWork(db),
            Mfa(db, hasher, protector, publisher, Options.Create(new TaskFlow.Api.Config.MfaOptions()), timeProvider),
            new TaskFlow.Api.Services.Auth.AttemptLimits.AttemptLimiter(timeProvider),
            hasher,
            publisher,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskFlow.Api.Services.Auth.PasswordRecovery.PasswordRecoveryService>.Instance,
            timeProvider);
    }
}
