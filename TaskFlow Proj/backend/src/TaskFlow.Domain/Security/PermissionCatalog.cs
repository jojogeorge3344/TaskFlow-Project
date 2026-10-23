namespace TaskFlow.Domain.Security;

/// <summary>
/// One permission, placed in the Module -> Screen -> Section -> Feature -> Action hierarchy.
/// </summary>
/// <param name="Key">The stable identifier code checks, for example <c>tasks.create</c>.</param>
/// <param name="Name">The feature and action as an administrator reads it, for example "Create tasks".</param>
/// <param name="Module">The top level. Each module appears once, whatever role uses it.</param>
/// <param name="Screen">The screen inside the module.</param>
/// <param name="Section">The tab or section on the screen, empty when the permission belongs to the screen itself.</param>
/// <param name="Action">View, Create, Edit, Delete, Assign, Export, Manage, Use or Access.</param>
/// <param name="GrantedToManager">Given to the Manager system role the first time the permission is seeded.</param>
/// <param name="GrantedToEmployee">Given to the Employee system role the first time the permission is seeded.</param>
/// <param name="IsAssignable">False for permissions only the Administrator may hold.</param>
/// <param name="AlwaysGranted">Every user has it and it cannot be switched off. Not stored as a grant.</param>
/// <param name="GrantToAllExistingRoles">When first seeded, every role that exists at that moment receives it, because the
/// feature was open to all signed-in users before it became a permission.</param>
/// <param name="GrantFrom">When first seeded, every role holding this key also receives the new permission, because the
/// feature used to be covered by that key.</param>
/// <param name="Requires">A permission that must also be held, normally the screen's View.</param>
/// <param name="ScopeGroup">Permissions sharing a group are levels of one selector; a higher rank includes the lower.</param>
/// <param name="ScopeRank">1 is the lowest level.</param>
public sealed record PermissionDefinition(
    string Key,
    string Name,
    string Description,
    string Module,
    string Screen,
    string Section,
    string Action,
    bool GrantedToManager = false,
    bool GrantedToEmployee = false,
    bool IsAssignable = true,
    bool AlwaysGranted = false,
    bool GrantToAllExistingRoles = false,
    string? GrantFrom = null,
    string? Requires = null,
    string? ScopeGroup = null,
    int ScopeRank = 0)
{
    /// <summary>The value stored in the <c>Group</c> column of the permission row.</summary>
    public string Group => Module;
}

/// <summary>
/// The single module-based permission catalogue. Each module appears once and lists every feature it offers, whichever
/// role uses it; roles only differ in which entries they hold. The Administrator always holds every assignable entry.
/// The Manager and Employee defaults reproduce the access those roles had before the catalogue was split into features.
/// <para>
/// Permissions listed in <see cref="EffectivePermissions.LegacyChildren"/> were replaced by finer ones. Roles that still
/// hold the old key keep exactly their old access through <see cref="EffectivePermissions"/>.
/// </para>
/// </summary>
public static class PermissionCatalog
{
    public const string Dashboard = "Dashboard";
    public const string Tasks = "Tasks";
    public const string Projects = "Projects";
    public const string Departments = "Departments";
    public const string Employees = "Employees";
    public const string Reports = "Reports and workload";
    public const string Mail = "Mail";
    public const string Notifications = "Notifications";
    public const string Assistant = "AI assistant";
    public const string Settings = "Settings";
    public const string RoleConfiguration = "Role configuration";
    public const string Profile = "Profile";

    private const string DashboardScope = "dashboard.view";
    private const string TaskScope = "tasks.view";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        // ---- Dashboard -------------------------------------------------------------------------------------------
        new(PermissionKeys.DashboardViewOwn, "View my dashboard", "Personal task summary, deadlines, performance and projects.",
            Dashboard, "Dashboard", "Overview", "View", AlwaysGranted: true, IsAssignable: false, ScopeGroup: DashboardScope, ScopeRank: 1),
        new(PermissionKeys.DashboardViewTeam, "View team dashboard", "Task statistics and team performance for the people the user manages.",
            Dashboard, "Dashboard", "Overview", "View", GrantedToManager: true, ScopeGroup: DashboardScope, ScopeRank: 2),
        new(PermissionKeys.DashboardViewOrganization, "View organization dashboard", "Task, department, project and employee statistics for the whole organization.",
            Dashboard, "Dashboard", "Overview", "View", ScopeGroup: DashboardScope, ScopeRank: 3),
        new(PermissionKeys.DashboardAnalytics, "View charts and reports", "Open the charts and analytics: status, trend, deadlines, workload and progress.",
            Dashboard, "Dashboard", "Analytics", "Access", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.DashboardFilters, "Use dashboard filters", "Filter dashboard analytics by department, project, employee and period.",
            Dashboard, "Dashboard", "Analytics", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Tasks -----------------------------------------------------------------------------------------------
        new(PermissionKeys.TasksViewOwn, "View my tasks", "See tasks the user created or is assigned to, on the list, board and details screens.",
            Tasks, "Task list", "", "View", AlwaysGranted: true, IsAssignable: false, ScopeGroup: TaskScope, ScopeRank: 1),
        new(PermissionKeys.TasksViewAll, "View all tasks", "See every task in the organization. Without it a user sees only tasks they created or are assigned to.",
            Tasks, "Task list", "", "View", GrantedToManager: true, ScopeGroup: TaskScope, ScopeRank: 2),
        new(PermissionKeys.TasksCreate, "Create tasks", "Create tasks, or copy an existing one, and assign them.",
            Tasks, "Task list", "", "Create", GrantedToManager: true),
        new(PermissionKeys.TasksDelete, "Delete tasks", "Delete a task from the list or the details screen.",
            Tasks, "Task list", "", "Delete"),
        new(PermissionKeys.TasksStatus, "Update task status", "Move a task between statuses, on the board or the details screen.",
            Tasks, "Task board", "", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.TasksUpdate, "Edit tasks", "Change the title, description, priority, deadline, reminders and other details of a task.",
            Tasks, "Task details", "Details", "Edit", GrantedToManager: true),
        new(PermissionKeys.TasksReassign, "Reassign tasks", "Move a task to another assignee.",
            Tasks, "Task details", "Details", "Assign", GrantedToManager: true),
        new(PermissionKeys.TasksRecipients, "Manage task recipients", "Choose who is notified about a task.",
            Tasks, "Task details", "Details", "Assign", GrantedToManager: true),
        new(PermissionKeys.TasksCommentCreate, "Post task comments", "Write comments in a task's discussion.",
            Tasks, "Task details", "Discussion", "Create", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.TasksActivityView, "View task history", "Open a task's activity history.",
            Tasks, "Task details", "Activity", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Projects --------------------------------------------------------------------------------------------
        new(PermissionKeys.ProjectsView, "View projects", "See projects, their members and their tasks.",
            Projects, "Project list", "", "View", GrantedToManager: true),
        new(PermissionKeys.ProjectsCreate, "Create projects", "Create a project.",
            Projects, "Project list", "", "Create", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsEdit, "Edit projects", "Rename a project and change its details.",
            Projects, "Project detail", "Details", "Edit", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsDelete, "Delete projects", "Delete a project.",
            Projects, "Project list", "", "Delete", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsMembers, "Manage project members", "Add and remove the people in a project.",
            Projects, "Project detail", "Members", "Assign", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.PositionsCreate, "Create positions", "Add a job position used when allocating people to projects.",
            Projects, "Project detail", "Members", "Create", Requires: PermissionKeys.ProjectsView),

        // ---- Departments -----------------------------------------------------------------------------------------
        new(PermissionKeys.DepartmentsView, "View departments", "See departments and the managers and employees in them.",
            Departments, "Department list", "", "View", GrantedToManager: true),
        new(PermissionKeys.DepartmentsCreate, "Create departments", "Create a department.",
            Departments, "Department list", "", "Create", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsEdit, "Edit departments", "Rename a department and change its details.",
            Departments, "Department list", "", "Edit", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsDelete, "Delete departments", "Delete a department.",
            Departments, "Department list", "", "Delete", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsMembers, "Manage department members", "Choose the managers and employees in a department.",
            Departments, "Department list", "Members", "Assign", Requires: PermissionKeys.DepartmentsView),

        // ---- Employees -------------------------------------------------------------------------------------------
        new(PermissionKeys.EmployeesView, "View employees", "See the employee list and employee details.",
            Employees, "Employee list", "", "View", GrantedToManager: true),
        new(PermissionKeys.EmployeesCreate, "Create employees", "Add an employee.",
            Employees, "Employee list", "", "Create", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesEdit, "Edit employees", "Change an employee's details.",
            Employees, "Employee list", "", "Edit", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesStatus, "Activate or deactivate employees", "Switch an employee account on or off.",
            Employees, "Employee list", "", "Edit", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesRoleAssign, "Assign roles", "Change which role an employee holds.",
            Employees, "Employee list", "", "Assign", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesMfaReset, "Reset two-step verification", "Reset an employee's authenticator setup.",
            Employees, "Employee list", "", "Manage", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesDelete, "Delete employees", "Permanently remove an employee account.",
            Employees, "Employee list", "", "Delete", Requires: PermissionKeys.EmployeesView),

        // ---- Reports and workload --------------------------------------------------------------------------------
        new(PermissionKeys.ReportsView, "View reports", "Open task reports and filter them.",
            Reports, "Reports", "", "View", GrantedToManager: true),
        new(PermissionKeys.ReportsExport, "Export reports", "Download a report as a file.",
            Reports, "Reports", "", "Export", GrantedToManager: true, GrantFrom: PermissionKeys.ReportsView, Requires: PermissionKeys.ReportsView),
        new(PermissionKeys.WorkloadView, "View workload", "Open the team workload view.",
            Reports, "Employee workload", "", "View", GrantedToManager: true),

        // ---- Mail ------------------------------------------------------------------
        new(PermissionKeys.MailRead, "Read mail", "Open the inbox, folders and messages.", Mail, "Mailbox", "", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailCompose, "Write and send mail", "Compose, save drafts, reply and forward.", Mail, "Mailbox", "", "Create", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailOrganize, "Organize mail", "Star, mark important, move to trash, restore and delete.", Mail, "Mailbox", "", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailAttachments, "Use mail attachments", "Upload and download attachments.", Mail, "Mailbox", "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Notifications ---------------------------------------------------------------------------------------
        new(PermissionKeys.NotificationsView, "View notifications", "Open the notification centre.", Notifications, "Notification centre", "", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.NotificationsManage, "Mark notifications read", "Mark one or all notifications as read.", Notifications, "Notification centre", "", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- AI assistant ----------------------------------------------------------------------------------------
        new(PermissionKeys.AssistantChat, "Chat with the assistant", "Ask the AI assistant questions and start new conversations.", Assistant, "Assistant", "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.AssistantActions, "Confirm assistant actions", "Approve or cancel actions the assistant proposes. The assistant can only do what the user's own permissions allow.", Assistant, "Assistant", "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Settings --------------------------------------------------------------------------------------------
        new(PermissionKeys.SettingsReminders, "Reminder settings", "View and change deadline reminder settings.",
            Settings, "Reminder settings", "", "Manage", GrantedToManager: true),
        new(PermissionKeys.SettingsApplication, "Application settings", "Change application-wide settings.",
            Settings, "Application settings", "", "Manage"),
        new(PermissionKeys.SettingsAiView, "View AI provider settings", "See which AI providers and models are configured.",
            Settings, "TFA settings", "AI providers", "View"),
        new(PermissionKeys.SettingsAiEdit, "Edit AI provider settings", "Configure AI providers, keys and models.",
            Settings, "TFA settings", "AI providers", "Edit", Requires: PermissionKeys.SettingsAiView),
        new(PermissionKeys.SettingsAiTest, "Test AI providers", "Run a connection test against a provider.",
            Settings, "TFA settings", "AI providers", "Use", Requires: PermissionKeys.SettingsAiView),
        new(PermissionKeys.SettingsAiRemoveModel, "Remove AI models", "Remove a configured model from a provider.",
            Settings, "TFA settings", "AI providers", "Delete", Requires: PermissionKeys.SettingsAiView),

        // ---- Role configuration ---------------------------------------------------------------
        new(PermissionKeys.RolesManage, "Manage roles and permissions", "Open Role Configuration and create, edit and delete custom roles. The Administrator role always has it; giving it to another role lets its users change what every custom role can do.",
            RoleConfiguration, "Role configuration", "", "Manage"),

        // ---- Profile (always on: sign-in and first-login set-up depend on it) ----------------------------------------------------------------------
        new(PermissionKeys.ProfileView, "View my profile", "Open the user's own profile.", Profile, "My profile", "Account", "View", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfileEdit, "Edit my personal information", "Update the user's own contact details.", Profile, "My profile", "Personal information", "Edit", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfilePassword, "Change my password", "Change the user's own password.", Profile, "My profile", "Security", "Edit", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfileMfa, "Manage my authenticator", "Set up two-step verification and regenerate backup codes.", Profile, "My profile", "Security", "Manage", AlwaysGranted: true, IsAssignable: false)
    ];

    /// <summary>Entries stored as grants. Always-on entries are not stored; every user simply has them.</summary>
    public static IEnumerable<PermissionDefinition> Stored => All.Where(definition => !definition.AlwaysGranted);
}
