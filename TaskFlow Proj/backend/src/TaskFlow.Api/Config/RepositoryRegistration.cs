using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Config;

/// <summary>The data-access layer: the database context, the unit of work, the module repositories and startup seeding.</summary>
public static class RepositoryRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Resolved when the context is first built (not while the app starts) so configuration added later, such as by the
        // integration tests, decides where the database goes.
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseSqlite(DatabasePathResolver.Resolve(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"),
                StorageRoot.Resolve(provider.GetRequiredService<IOptions<StorageOptions>>().Value.Root))));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Settings
        services.AddScoped<TaskFlow.Api.Repositories.Settings.ApplicationSettings.IApplicationSettingsRepository, TaskFlow.Api.Repositories.Settings.ApplicationSettings.ApplicationSettingsRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Settings.RoleConfiguration.Roles.IRoleRepository, TaskFlow.Api.Repositories.Settings.RoleConfiguration.Roles.RoleRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Settings.RoleConfiguration.Permissions.IPermissionRepository, TaskFlow.Api.Repositories.Settings.RoleConfiguration.Permissions.PermissionRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig.IAiProviderConfigRepository, TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig.AiProviderConfigRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels.IAiModelRepository, TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels.AiModelRepository>();

        // Management
        services.AddScoped<TaskFlow.Api.Repositories.Management.EmployeeManagement.Employees.IEmployeeRepository, TaskFlow.Api.Repositories.Management.EmployeeManagement.Employees.EmployeeRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Management.DepartmentManagement.IDepartmentRepository, TaskFlow.Api.Repositories.Management.DepartmentManagement.DepartmentRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Management.ProjectManagement.Projects.IProjectRepository, TaskFlow.Api.Repositories.Management.ProjectManagement.Projects.ProjectRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Management.ProjectManagement.Positions.IPositionRepository, TaskFlow.Api.Repositories.Management.ProjectManagement.Positions.PositionRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations.IUserAllocationRepository, TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations.UserAllocationRepository>();

        // Monitoring
        services.AddScoped<TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks.ITaskRepository, TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks.TaskRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload.IWorkloadRepository, TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload.WorkloadRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Monitoring.TaskManagement.Discussion.ITaskCommentRepository, TaskFlow.Api.Repositories.Monitoring.TaskManagement.Discussion.TaskCommentRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Monitoring.TaskManagement.Activity.ITaskActivityRepository, TaskFlow.Api.Repositories.Monitoring.TaskManagement.Activity.TaskActivityRepository>();

        // Communication
        services.AddScoped<TaskFlow.Api.Repositories.Communication.Mail.Messages.IMailRepository, TaskFlow.Api.Repositories.Communication.Mail.Messages.MailRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Communication.Mail.Attachments.IMailAttachmentRepository, TaskFlow.Api.Repositories.Communication.Mail.Attachments.MailAttachmentRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Communication.Notification.Notifications.INotificationRepository, TaskFlow.Api.Repositories.Communication.Notification.Notifications.NotificationRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Communication.Notification.Reminders.IReminderRepository, TaskFlow.Api.Repositories.Communication.Notification.Reminders.ReminderRepository>();

        // Dashboard
        services.AddScoped<TaskFlow.Api.Repositories.Dashboard.Overview.IDashboardRepository, TaskFlow.Api.Repositories.Dashboard.Overview.DashboardRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Dashboard.Analytics.IAnalyticsRepository, TaskFlow.Api.Repositories.Dashboard.Analytics.AnalyticsRepository>();

        // Reports
        services.AddScoped<TaskFlow.Api.Repositories.Reports.IReportRepository, TaskFlow.Api.Repositories.Reports.ReportRepository>();

        // Assistant
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.Conversations.IAssistantConversationRepository, TaskFlow.Api.Repositories.Assistant.Conversations.AssistantConversationRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.Messages.IAssistantMessageRepository, TaskFlow.Api.Repositories.Assistant.Messages.AssistantMessageRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.PendingActions.IAssistantPendingActionRepository, TaskFlow.Api.Repositories.Assistant.PendingActions.AssistantPendingActionRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.Audits.IAssistantAuditRepository, TaskFlow.Api.Repositories.Assistant.Audits.AssistantAuditRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.People.IAssistantDirectoryRepository, TaskFlow.Api.Repositories.Assistant.People.AssistantDirectoryRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Assistant.Documents.IDocumentIndexRepository, TaskFlow.Api.Repositories.Assistant.Documents.DocumentIndexRepository>();

        // Auth
        services.AddScoped<TaskFlow.Api.Repositories.Auth.RefreshTokens.IRefreshTokenRepository, TaskFlow.Api.Repositories.Auth.RefreshTokens.RefreshTokenRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Auth.Accounts.IAccountRepository, TaskFlow.Api.Repositories.Auth.Accounts.AccountRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Auth.Mfa.IMfaBackupCodeRepository, TaskFlow.Api.Repositories.Auth.Mfa.MfaBackupCodeRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Auth.PasswordRecovery.IPasswordResetGrantRepository, TaskFlow.Api.Repositories.Auth.PasswordRecovery.PasswordResetGrantRepository>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<AccessControlSeeder>();
        services.AddScoped<TaskFlow.Api.Persistence.Seeders.AiProviderSeeder>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<TaskFlow.Api.Persistence.Seeders.SettingsInitializer>();

        return services;
    }
}
