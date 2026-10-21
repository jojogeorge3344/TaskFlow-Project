namespace TaskFlow.Domain.Security;

/// <summary>One permission and which system roles receive it the first time it is seeded.</summary>
public sealed record PermissionDefinition(
    string Key,
    string Name,
    string Description,
    string Group,
    bool GrantedToManager,
    bool GrantedToEmployee,
    bool IsAssignable = true);

/// <summary>
/// The fixed permission catalog. The Administrator always holds every entry. The Manager and Employee columns are the
/// access those roles had before permissions existed, taken from the old role checks:
/// <code>
/// permission                     endpoints it guards                                   Mgr  Emp
/// dashboard.admin                GET dashboard/admin                                    -    -
/// dashboard.manager              GET dashboard/manager                                  x    -
/// projects.view                  GET projects, projects/{id}, .../employees             x    -
/// projects.manage                POST/PUT/DELETE projects                               -    -
/// departments.view               GET departments, departments/{id}, .../members         x    -
/// departments.manage             POST/PUT/DELETE departments, PUT .../members           -    -
/// employees.view                 GET users                                              x    -
/// employees.manage               POST/PUT users, PATCH users/{id}/status                -    -
/// tasks.view.all                 organization-wide task visibility (row scope)          x    -
/// tasks.create                   POST tasks                                             x    -
/// tasks.update                   PUT tasks/{id}                                         x    -
/// tasks.reassign                 POST tasks/{id}/reassign                               x    -
/// tasks.recipients.manage        PUT tasks/{id}/recipients                              x    -
/// reports.view                   GET reports, reports/export                            x    -
/// workload.view                  GET workload                                           x    -
/// settings.reminders.manage      GET/PUT settings/reminders                             x    -
/// settings.application.manage    PUT settings/application                               -    -
/// settings.ai.manage             assistant provider config and tests                    -    -
/// roles.manage                   roles and permissions endpoints (not assignable)       -    -
/// </code>
/// Endpoints open to every signed-in user (profile, mail, notifications, the assistant chat, reading and commenting on
/// tasks the caller may see, the employee dashboard) need no permission.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(PermissionKeys.DashboardAdmin, "Administrator dashboard", "Open the organization-wide administrator dashboard.", "Dashboards", false, false),
        new(PermissionKeys.DashboardManager, "Manager dashboard", "Open the manager dashboard for the team.", "Dashboards", true, false),

        new(PermissionKeys.ProjectsView, "View projects", "See projects and the employees in them.", "Projects", true, false),
        new(PermissionKeys.ProjectsManage, "Manage projects", "Create, rename and delete projects.", "Projects", false, false),

        new(PermissionKeys.DepartmentsView, "View departments", "See departments and the managers and employees in them.", "Departments", true, false),
        new(PermissionKeys.DepartmentsManage, "Manage departments", "Create, rename and delete departments and manage their members.", "Departments", false, false),

        new(PermissionKeys.EmployeesView, "View employees", "See the employee list and employee details.", "Employees", true, false),
        new(PermissionKeys.EmployeesManage, "Manage employees", "Create and edit employees, activate or deactivate them and change their role.", "Employees", false, false),

        new(PermissionKeys.TasksViewAll, "View all tasks", "See every task in the organization. Without it a user sees only tasks they created or are assigned to.", "Tasks", true, false),
        new(PermissionKeys.TasksCreate, "Create tasks", "Create tasks and assign them.", "Tasks", true, false),
        new(PermissionKeys.TasksUpdate, "Edit tasks", "Change the title, description, priority, deadline and other details of a task.", "Tasks", true, false),
        new(PermissionKeys.TasksReassign, "Reassign tasks", "Move a task to another assignee.", "Tasks", true, false),
        new(PermissionKeys.TasksRecipients, "Manage task recipients", "Choose who is notified about a task.", "Tasks", true, false),

        new(PermissionKeys.ReportsView, "View and export reports", "Open task reports and export them.", "Reports and workload", true, false),
        new(PermissionKeys.WorkloadView, "View workload", "Open the team workload view.", "Reports and workload", true, false),

        new(PermissionKeys.SettingsReminders, "Reminder settings", "View and change deadline reminder settings.", "Settings", true, false),
        new(PermissionKeys.SettingsApplication, "Application settings", "Change application-wide settings.", "Settings", false, false),
        new(PermissionKeys.SettingsAi, "AI assistant settings", "Configure and test AI providers for the assistant.", "Settings", false, false),

        new(PermissionKeys.RolesManage, "Manage roles and permissions", "Create, edit and delete roles and change which role a user holds. Administrator only.", "Access control", false, false, IsAssignable: false)
    ];
}
