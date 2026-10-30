using TaskFlow.Api.Authorization;
using TaskFlow.Application.Auth;

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
        services.AddSingleton<IAttemptLimiter, TaskFlow.Infrastructure.Services.AttemptLimiter>();
        services.Configure<TaskFlow.Application.Mfa.MfaOptions>(configuration.GetSection(TaskFlow.Application.Mfa.MfaOptions.SectionName));
        services.AddScoped<TaskFlow.Application.Mfa.IMfaService, TaskFlow.Application.Mfa.MfaService>();

        // Dashboard
        services.AddScoped<TaskFlow.Application.Dashboard.IDashboardService, TaskFlow.Infrastructure.Services.DashboardService>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IDashboardScope, TaskFlow.Infrastructure.Analytics.AdminDashboardScope>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IManagerDashboardScope, TaskFlow.Infrastructure.Analytics.ManagerDashboardScope>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IManagerAnalyticsService, TaskFlow.Infrastructure.Analytics.ManagerAnalyticsService>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IAdminAnalyticsService, TaskFlow.Infrastructure.Analytics.AdminAnalyticsService>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IEmployeeDashboardScope, TaskFlow.Infrastructure.Analytics.EmployeeDashboardScope>();
        services.AddScoped<TaskFlow.Application.Dashboard.Analytics.IEmployeeAnalyticsService, TaskFlow.Infrastructure.Analytics.EmployeeAnalyticsService>();

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
        services.AddScoped<TaskFlow.Application.Reports.IReportService, TaskFlow.Infrastructure.Services.ReportService>();

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
