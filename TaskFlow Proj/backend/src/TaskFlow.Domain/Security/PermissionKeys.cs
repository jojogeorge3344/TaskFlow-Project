namespace TaskFlow.Domain.Security;

/// <summary>The permission keys code checks. Each one is listed in <see cref="PermissionCatalog"/>, except the
/// <c>Legacy*</c> keys, which only exist as grants roles already hold (see <see cref="EffectivePermissions"/>).</summary>
public static class PermissionKeys
{
    // Dashboard. The three view keys are one scope selector: Own < Team < Organization.
    public const string DashboardViewOwn = "dashboard.view.own";
    public const string DashboardViewTeam = "dashboard.view.team";
    public const string DashboardViewOrganization = "dashboard.view.organization";
    public const string DashboardAnalytics = "dashboard.analytics.view";
    public const string DashboardFilters = "dashboard.filters.use";

    public const string LegacyDashboardAdmin = "dashboard.admin";
    public const string LegacyDashboardManager = "dashboard.manager";

    // Tasks. View own < view all is a scope selector.
    public const string TasksViewOwn = "tasks.view.own";
    public const string TasksViewAll = "tasks.view.all";
    public const string TasksCreate = "tasks.create";
    public const string TasksUpdate = "tasks.update";
    public const string TasksDelete = "tasks.delete";
    public const string TasksStatus = "tasks.status.change";
    public const string TasksReassign = "tasks.reassign";
    public const string TasksRecipients = "tasks.recipients.manage";
    public const string TasksCommentCreate = "tasks.comment.create";
    public const string TasksCommentEdit = "tasks.comment.edit";
    public const string TasksCommentDelete = "tasks.comment.delete";
    public const string TasksActivityView = "tasks.activity.view";

    // Projects
    public const string ProjectsView = "projects.view";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsDelete = "projects.delete";
    public const string ProjectsMembers = "projects.members.manage";
    public const string PositionsCreate = "positions.create";
    public const string LegacyProjectsManage = "projects.manage";

    // Departments
    public const string DepartmentsView = "departments.view";
    public const string DepartmentsCreate = "departments.create";
    public const string DepartmentsEdit = "departments.edit";
    public const string DepartmentsDelete = "departments.delete";
    public const string DepartmentsMembers = "departments.members.manage";
    public const string LegacyDepartmentsManage = "departments.manage";

    // Employees
    public const string EmployeesView = "employees.view";
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesEdit = "employees.edit";
    public const string EmployeesStatus = "employees.status.change";
    public const string EmployeesDelete = "employees.delete";
    public const string EmployeesRoleAssign = "employees.role.assign";
    public const string EmployeesMfaReset = "employees.mfa.reset";
    public const string LegacyEmployeesManage = "employees.manage";

    // Reports and workload
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string WorkloadView = "workload.view";
    public const string WorkloadFilters = "workload.filters.use";
    public const string WorkloadTasksOpen = "workload.tasks.open";

    // Board
    public const string BoardView = "board.view";
    public const string BoardFilters = "board.filters.use";

    // Settings
    public const string SettingsReminders = "settings.reminders.manage";
    public const string SettingsApplication = "settings.application.manage";
    public const string SettingsAiView = "settings.ai.view";
    public const string SettingsAiEdit = "settings.ai.edit";
    public const string SettingsAiTest = "settings.ai.test";
    public const string SettingsAiRemoveModel = "settings.ai.model.remove";
    public const string LegacySettingsAi = "settings.ai.manage";

    // Role configuration (Administrator only)
    public const string RolesManage = "roles.manage";
    public const string RolesAdministratorAssign = "roles.administrator.assign";

    // Always-on features every signed-in user has. Listed so the catalogue is complete; they cannot be switched off.
    public const string ProfileView = "profile.view";
    public const string ProfileEdit = "profile.edit";
    public const string ProfilePassword = "profile.password.change";
    public const string ProfileMfa = "profile.mfa.manage";
    public const string MailRead = "mail.read";
    public const string MailCompose = "mail.compose";
    public const string MailOrganize = "mail.organize";
    public const string MailAttachments = "mail.attachments";
    public const string NotificationsView = "notifications.view";
    public const string NotificationsManage = "notifications.manage";
    public const string AssistantChat = "assistant.chat";
    public const string AssistantActions = "assistant.actions.confirm";
}
