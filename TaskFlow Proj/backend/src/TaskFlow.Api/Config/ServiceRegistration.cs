using TaskFlow.Api.Services.Interfaces.Auth.PasswordRecovery;
using TaskFlow.Api.Services.Interfaces.Auth;
using TaskFlow.Api.Services.Auth.PasswordRecovery;
using TaskFlow.Api.Services.Auth;
using TaskFlow.Api.Authorization;

namespace TaskFlow.Api.Config;

/// <summary>The service layer: every service that holds business rules, grouped by module.</summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Auth
        services.AddScoped<IAuthService, AuthService>();
        // Password recovery from the login page: an authenticator code buys a single-use reset token.
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddScoped<ISessionService, SessionService>();

        // Profile
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Profile.IProfileService, TaskFlow.Api.Services.Profile.ProfileService>();
        services.AddSingleton<IAttemptLimiter, TaskFlow.Api.Services.Auth.AttemptLimiter>();
        services.Configure<TaskFlow.Api.Config.MfaOptions>(configuration.GetSection(TaskFlow.Api.Config.MfaOptions.SectionName));
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Auth.Mfa.IMfaService, TaskFlow.Api.Services.Auth.Mfa.MfaService>();

        // Dashboard
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.IDashboardService, TaskFlow.Api.Services.Dashboard.DashboardService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.AdminDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IManagerDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.ManagerDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IManagerAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.ManagerAnalyticsService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IAdminAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.AdminAnalyticsService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IEmployeeDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.EmployeeDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Dashboard.Analytics.IEmployeeAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.EmployeeAnalyticsService>();

        // Monitoring
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Monitoring.TaskManagement.ITaskService, TaskFlow.Api.Services.Monitoring.TaskManagement.TaskService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Monitoring.TaskManagement.ITaskAccessService, TaskFlow.Api.Services.Monitoring.TaskManagement.TaskAccessService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Monitoring.TaskManagement.Discussion.ITaskCommentService, TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion.TaskCommentService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Monitoring.TaskManagement.Activity.ITaskActivityService, TaskFlow.Api.Services.Monitoring.TaskManagement.Activity.TaskActivityService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Monitoring.EmployeeWorkload.IWorkloadService, TaskFlow.Api.Services.Monitoring.EmployeeWorkload.WorkloadService>();

        // Management
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement.IEmployeeService, TaskFlow.Api.Services.Management.EmployeeManagement.EmployeeService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement.Allocations.IUserAllocationService, TaskFlow.Api.Services.Management.EmployeeManagement.Allocations.UserAllocationService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Management.ProjectManagement.IProjectService, TaskFlow.Api.Services.Management.ProjectManagement.ProjectService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Management.ProjectManagement.Positions.IPositionService, TaskFlow.Api.Services.Management.ProjectManagement.Positions.PositionService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Management.DepartmentManagement.IDepartmentService, TaskFlow.Api.Services.Management.DepartmentManagement.DepartmentService>();

        // Communication
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Communication.Mail.IMailService, TaskFlow.Api.Services.Communication.Mail.MailService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Communication.Mail.Attachments.IMailAttachmentService, TaskFlow.Api.Services.Communication.Mail.Attachments.MailAttachmentService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Communication.Notification.INotificationPublisher, TaskFlow.Api.Services.Communication.Notification.NotificationPublisher>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Communication.Notification.INotificationService, TaskFlow.Api.Services.Communication.Notification.NotificationService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Communication.Notification.Reminders.IReminderService, TaskFlow.Api.Services.Communication.Notification.Reminders.ReminderService>();
        services.Configure<TaskFlow.Api.Config.ReminderEngineOptions>(
            configuration.GetSection(TaskFlow.Api.Config.ReminderEngineOptions.SectionName));
        services.AddHostedService<TaskFlow.Api.Services.Communication.Notification.Reminders.ReminderSchedulerService>();

        // Reports
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Reports.IReportService, TaskFlow.Api.Services.Reports.ReportService>();

        // Settings
        services.Configure<TaskFlow.Api.Config.ApplicationOptions>(
            configuration.GetSection(TaskFlow.Api.Config.ApplicationOptions.SectionName));
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings.IAppClock, TaskFlow.Api.Services.Settings.ApplicationSettings.AppClock>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.ReminderSettings.IReminderSettingsService, TaskFlow.Api.Services.Settings.ReminderSettings.ReminderSettingsService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings.IApplicationSettingsService, TaskFlow.Api.Services.Settings.ApplicationSettings.ApplicationSettingsService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.RoleConfiguration.IRoleService, TaskFlow.Api.Services.Settings.RoleConfiguration.RoleService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.TfaSettings.IAiProviderConfigService, TaskFlow.Api.Services.Settings.TfaSettings.AiProviderConfigService>();
        services.AddScoped<TaskFlow.Api.Services.Interfaces.Settings.TfaSettings.IAiProviderConnectionTester, TaskFlow.Api.Services.Settings.TfaSettings.AiProviderConnectionTester>();

        // Assistant
        services.AddSingleton<TaskFlow.Application.Assistant.IAssistantRateLimiter, TaskFlow.Infrastructure.Assistant.AssistantRateLimiter>();
        services.AddSingleton<TaskFlow.Application.Assistant.IHelpKnowledgeBase, TaskFlow.Infrastructure.Assistant.HelpKnowledgeBase>();
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantAuthorizer, AssistantAuthorizer>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantTaskReader>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantUserResolver>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantManagerResolver>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.AssistantToolGate>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.GetMyTasksTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.PrioritiseTasksTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.SearchHelpTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.FindUserTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ExplainPasswordChangeTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ContactAdminGuidanceTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.DraftMailTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ProposeProfileUpdateTool>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.IAssistantTool, TaskFlow.Infrastructure.Assistant.ProposeRoleChangeTool>();
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantToolbox, TaskFlow.Infrastructure.Assistant.AssistantToolbox>();
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantAuditLog, TaskFlow.Infrastructure.Assistant.AssistantAuditLog>();
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantActionService, TaskFlow.Infrastructure.Assistant.AssistantActionService>();
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantActionCards>(
            provider => provider.GetRequiredService<TaskFlow.Application.Assistant.IAssistantActionService>());
        services.AddScoped<TaskFlow.Application.Assistant.IAssistantService, TaskFlow.Infrastructure.Assistant.AssistantService>();

        return services;
    }
}
