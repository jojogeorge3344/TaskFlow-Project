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
        services.AddScoped<TaskFlow.Application.Tasks.ITaskService, TaskFlow.Infrastructure.Services.TaskService>();
        services.AddScoped<TaskFlow.Application.Tasks.ITaskCommentService, TaskFlow.Infrastructure.Services.TaskCommentService>();
        services.AddScoped<TaskFlow.Application.Tasks.ITaskActivityService, TaskFlow.Infrastructure.Services.TaskActivityService>();
        services.AddScoped<TaskFlow.Application.Workload.IWorkloadService, TaskFlow.Infrastructure.Services.WorkloadService>();

        // Management
        services.AddScoped<TaskFlow.Application.Users.IUserService, TaskFlow.Infrastructure.Services.UserService>();
        services.AddScoped<TaskFlow.Application.Users.IUserAllocationService, TaskFlow.Infrastructure.Services.UserAllocationService>();
        services.AddScoped<TaskFlow.Application.Projects.IProjectService, TaskFlow.Infrastructure.Services.ProjectService>();
        services.AddScoped<TaskFlow.Application.Projects.IPositionService, TaskFlow.Infrastructure.Services.PositionService>();
        services.AddScoped<TaskFlow.Application.Departments.IDepartmentService, TaskFlow.Infrastructure.Services.DepartmentService>();

        // Communication
        services.AddScoped<TaskFlow.Application.Mail.IMailService, TaskFlow.Infrastructure.Services.MailService>();
        services.AddScoped<TaskFlow.Application.Mail.IMailAttachmentService, TaskFlow.Infrastructure.Services.MailAttachmentService>();
        services.AddScoped<TaskFlow.Application.Tasks.INotificationPublisher, TaskFlow.Infrastructure.Services.NotificationPublisher>();
        services.AddScoped<TaskFlow.Application.Notifications.INotificationService, TaskFlow.Infrastructure.Services.NotificationService>();
        services.AddScoped<TaskFlow.Application.Reminders.IReminderService, TaskFlow.Infrastructure.Services.ReminderService>();
        services.Configure<TaskFlow.Infrastructure.Services.ReminderEngineOptions>(
            configuration.GetSection(TaskFlow.Infrastructure.Services.ReminderEngineOptions.SectionName));
        services.AddHostedService<TaskFlow.Infrastructure.Services.ReminderSchedulerService>();

        // Reports
        services.AddScoped<TaskFlow.Application.Reports.IReportService, TaskFlow.Infrastructure.Services.ReportService>();

        // Settings
        services.Configure<TaskFlow.Infrastructure.Settings.ApplicationOptions>(
            configuration.GetSection(TaskFlow.Infrastructure.Settings.ApplicationOptions.SectionName));
        services.AddScoped<TaskFlow.Application.Settings.IAppClock, TaskFlow.Infrastructure.Settings.AppClock>();
        services.AddScoped<TaskFlow.Application.Settings.IReminderSettingsService, TaskFlow.Infrastructure.Settings.ReminderSettingsService>();
        services.AddScoped<TaskFlow.Application.Settings.IApplicationSettingsService, TaskFlow.Infrastructure.Settings.ApplicationSettingsService>();
        services.AddScoped<TaskFlow.Application.Roles.IRoleService, TaskFlow.Infrastructure.Services.RoleService>();
        services.AddScoped<TaskFlow.Application.Assistant.IAiProviderConfigService, TaskFlow.Infrastructure.Assistant.AiProviderConfigService>();
        services.AddScoped<TaskFlow.Application.Assistant.IAiProviderConnectionTester, TaskFlow.Infrastructure.Assistant.AiProviderConnectionTester>();

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
