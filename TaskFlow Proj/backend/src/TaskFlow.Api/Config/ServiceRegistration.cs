using TaskFlow.Api.Services.Auth.PasswordRecovery;
using TaskFlow.Api.Services.Auth.AttemptLimits;
using TaskFlow.Api.Services.Auth.Login;
using TaskFlow.Api.Services.Auth.Sessions;
using TaskFlow.Api.Authorization.Assistant;

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
        services.AddScoped<TaskFlow.Api.Services.Profile.IProfileService, TaskFlow.Api.Services.Profile.ProfileService>();
        services.AddSingleton<IAttemptLimiter, TaskFlow.Api.Services.Auth.AttemptLimits.AttemptLimiter>();
        services.Configure<TaskFlow.Api.Config.MfaOptions>(configuration.GetSection(TaskFlow.Api.Config.MfaOptions.SectionName));
        services.AddScoped<TaskFlow.Api.Services.Auth.Mfa.IMfaService, TaskFlow.Api.Services.Auth.Mfa.MfaService>();

        // Dashboard
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Overview.IDashboardService, TaskFlow.Api.Services.Dashboard.Overview.DashboardService>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Scopes.IDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.Scopes.AdminDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Scopes.IManagerDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.Scopes.ManagerDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Manager.IManagerAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.Manager.ManagerAnalyticsService>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Admin.IAdminAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.Admin.AdminAnalyticsService>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Scopes.IEmployeeDashboardScope, TaskFlow.Api.Services.Dashboard.Analytics.Scopes.EmployeeDashboardScope>();
        services.AddScoped<TaskFlow.Api.Services.Dashboard.Analytics.Employee.IEmployeeAnalyticsService, TaskFlow.Api.Services.Dashboard.Analytics.Employee.EmployeeAnalyticsService>();

        // Monitoring
        services.AddScoped<TaskFlow.Api.Services.Monitoring.TaskManagement.Tasks.ITaskService, TaskFlow.Api.Services.Monitoring.TaskManagement.Tasks.TaskService>();
        services.AddScoped<TaskFlow.Api.Services.Monitoring.TaskManagement.Access.ITaskAccessService, TaskFlow.Api.Services.Monitoring.TaskManagement.Access.TaskAccessService>();
        services.AddScoped<TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion.ITaskCommentService, TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion.TaskCommentService>();
        services.AddScoped<TaskFlow.Api.Services.Monitoring.TaskManagement.Activity.ITaskActivityService, TaskFlow.Api.Services.Monitoring.TaskManagement.Activity.TaskActivityService>();
        services.AddScoped<TaskFlow.Api.Services.Monitoring.EmployeeWorkload.IWorkloadService, TaskFlow.Api.Services.Monitoring.EmployeeWorkload.WorkloadService>();

        // Management
        services.AddScoped<TaskFlow.Api.Services.Management.EmployeeManagement.Employees.IEmployeeService, TaskFlow.Api.Services.Management.EmployeeManagement.Employees.EmployeeService>();
        services.AddScoped<TaskFlow.Api.Services.Management.EmployeeManagement.Allocations.IUserAllocationService, TaskFlow.Api.Services.Management.EmployeeManagement.Allocations.UserAllocationService>();
        services.AddScoped<TaskFlow.Api.Services.Management.ProjectManagement.Projects.IProjectService, TaskFlow.Api.Services.Management.ProjectManagement.Projects.ProjectService>();
        services.AddScoped<TaskFlow.Api.Services.Management.ProjectManagement.Positions.IPositionService, TaskFlow.Api.Services.Management.ProjectManagement.Positions.PositionService>();
        services.AddScoped<TaskFlow.Api.Services.Management.DepartmentManagement.IDepartmentService, TaskFlow.Api.Services.Management.DepartmentManagement.DepartmentService>();

        // Communication
        services.AddScoped<TaskFlow.Api.Services.Communication.Mail.Messages.IMailService, TaskFlow.Api.Services.Communication.Mail.Messages.MailService>();
        services.AddScoped<TaskFlow.Api.Services.Communication.Mail.Attachments.IMailAttachmentService, TaskFlow.Api.Services.Communication.Mail.Attachments.MailAttachmentService>();
        services.AddScoped<TaskFlow.Api.Services.Communication.Notification.Publishing.INotificationPublisher, TaskFlow.Api.Services.Communication.Notification.Publishing.NotificationPublisher>();
        services.AddScoped<TaskFlow.Api.Services.Communication.Notification.Notifications.INotificationService, TaskFlow.Api.Services.Communication.Notification.Notifications.NotificationService>();
        services.AddScoped<TaskFlow.Api.Services.Communication.Notification.Reminders.IReminderService, TaskFlow.Api.Services.Communication.Notification.Reminders.ReminderService>();
        services.Configure<TaskFlow.Api.Config.ReminderEngineOptions>(
            configuration.GetSection(TaskFlow.Api.Config.ReminderEngineOptions.SectionName));
        services.AddHostedService<TaskFlow.Api.Services.Communication.Notification.Reminders.ReminderSchedulerService>();

        // Reports
        services.AddScoped<TaskFlow.Api.Services.Reports.IReportService, TaskFlow.Api.Services.Reports.ReportService>();

        // Settings
        services.Configure<TaskFlow.Api.Config.ApplicationOptions>(
            configuration.GetSection(TaskFlow.Api.Config.ApplicationOptions.SectionName));
        services.AddScoped<TaskFlow.Api.Services.Settings.ApplicationSettings.Clock.IAppClock, TaskFlow.Api.Services.Settings.ApplicationSettings.Clock.AppClock>();
        services.AddScoped<TaskFlow.Api.Services.Settings.ReminderSettings.IReminderSettingsService, TaskFlow.Api.Services.Settings.ReminderSettings.ReminderSettingsService>();
        services.AddScoped<TaskFlow.Api.Services.Settings.ApplicationSettings.General.IApplicationSettingsService, TaskFlow.Api.Services.Settings.ApplicationSettings.General.ApplicationSettingsService>();
        services.AddScoped<TaskFlow.Api.Services.Settings.RoleConfiguration.IRoleService, TaskFlow.Api.Services.Settings.RoleConfiguration.RoleService>();
        services.AddScoped<TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig.IAiProviderConfigService, TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig.AiProviderConfigService>();
        services.AddScoped<TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest.IAiProviderConnectionTester, TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest.AiProviderConnectionTester>();

        // Assistant
        services.AddSingleton<TaskFlow.Api.Services.Assistant.RateLimits.IAssistantRateLimiter, TaskFlow.Api.Services.Assistant.RateLimits.AssistantRateLimiter>();
        services.AddSingleton<TaskFlow.Api.Services.Assistant.HelpKnowledge.IHelpKnowledgeBase, TaskFlow.Api.Services.Assistant.HelpKnowledge.HelpKnowledgeBase>();
        services.AddScoped<TaskFlow.Api.Authorization.Assistant.IAssistantAuthorizer, AssistantAuthorizer>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.AssistantTaskReader>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.AssistantUserResolver>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.AssistantManagerResolver>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Toolbox.AssistantToolGate>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.GetMyTasksTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.PrioritiseTasksTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.SearchHelpTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.FindUserTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.ExplainPasswordChangeTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.ContactAdminGuidanceTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.DraftMailTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.ProposeProfileUpdateTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Tools.IAssistantTool, TaskFlow.Api.Services.Assistant.Tools.ProposeRoleChangeTool>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Toolbox.IAssistantToolbox, TaskFlow.Api.Services.Assistant.Toolbox.AssistantToolbox>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.AuditLog.IAssistantAuditLog, TaskFlow.Api.Services.Assistant.AuditLog.AssistantAuditLog>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Actions.IAssistantActionService, TaskFlow.Api.Services.Assistant.Actions.AssistantActionService>();
        services.AddScoped<TaskFlow.Api.Services.Assistant.Actions.IAssistantActionCards>(
            provider => provider.GetRequiredService<TaskFlow.Api.Services.Assistant.Actions.IAssistantActionService>());
        services.AddScoped<TaskFlow.Api.Services.Assistant.Chat.IAssistantService, TaskFlow.Api.Services.Assistant.Chat.AssistantService>();

        return services;
    }
}
