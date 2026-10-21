namespace TaskFlow.Domain.Security;

/// <summary>The permission keys code checks. Each one is listed in <see cref="PermissionCatalog"/>.</summary>
public static class PermissionKeys
{
    public const string DashboardAdmin = "dashboard.admin";
    public const string DashboardManager = "dashboard.manager";

    public const string ProjectsView = "projects.view";
    public const string ProjectsManage = "projects.manage";

    public const string DepartmentsView = "departments.view";
    public const string DepartmentsManage = "departments.manage";

    public const string EmployeesView = "employees.view";
    public const string EmployeesManage = "employees.manage";

    public const string TasksViewAll = "tasks.view.all";
    public const string TasksCreate = "tasks.create";
    public const string TasksUpdate = "tasks.update";
    public const string TasksReassign = "tasks.reassign";
    public const string TasksRecipients = "tasks.recipients.manage";

    public const string ReportsView = "reports.view";
    public const string WorkloadView = "workload.view";

    public const string SettingsReminders = "settings.reminders.manage";
    public const string SettingsApplication = "settings.application.manage";
    public const string SettingsAi = "settings.ai.manage";

    public const string RolesManage = "roles.manage";
}
