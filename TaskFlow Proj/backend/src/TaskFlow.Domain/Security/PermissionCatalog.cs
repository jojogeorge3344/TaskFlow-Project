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
    // Top-level modules, in the order the Role Configuration tree lists them. Each is one contiguous block below.
    public const string Dashboard = "Dashboard";
    public const string Monitoring = "TaskFlow Monitoring";
    public const string Management = "TaskFlow Management";
    public const string Communication = "Communication Management";
    public const string Report = "Report";
    public const string Settings = "Settings";
    public const string Profile = "My Profile";
    public const string Assistant = "TFA assistant";

    // Submodules (the Screen level of the tree) that more than one entry names.
    private const string TaskManagement = "Task Management";
    private const string Board = "Board";
    private const string EmployeeWorkload = "Employee Workload";
    private const string EmployeeManagement = "Employee Management";
    private const string ProjectManagement = "Project Management";
    private const string DepartmentManagement = "Department Management";

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

        // ---- TaskFlow Monitoring > Task Management ---------------------------------------------------------------
        new(PermissionKeys.TasksViewOwn, "View my tasks", "See tasks the user created or is assigned to, on the list, board and details screens.",
            Monitoring, TaskManagement, "Task list", "View", AlwaysGranted: true, IsAssignable: false, ScopeGroup: TaskScope, ScopeRank: 1),
        new(PermissionKeys.TasksViewAll, "View all tasks", "See every task in the organization. Without it a user sees only tasks they created or are assigned to.",
            Monitoring, TaskManagement, "Task list", "View", GrantedToManager: true, ScopeGroup: TaskScope, ScopeRank: 2),
        new(PermissionKeys.TasksCreate, "Create tasks", "Create tasks, or copy an existing one, and assign them.",
            Monitoring, TaskManagement, "Task list", "Create", GrantedToManager: true),
        new(PermissionKeys.TasksDelete, "Delete tasks", "Delete a task from the list or the details screen.",
            Monitoring, TaskManagement, "Task list", "Delete"),
        new(PermissionKeys.TasksUpdate, "Edit tasks", "Change the title, description, priority, deadline, reminders and other details of a task.",
            Monitoring, TaskManagement, "Details", "Edit", GrantedToManager: true),
        new(PermissionKeys.TasksReassign, "Reassign tasks", "Move a task to another assignee.",
            Monitoring, TaskManagement, "Details", "Assign", GrantedToManager: true),
        new(PermissionKeys.TasksRecipients, "Manage task recipients", "Choose who is notified about a task.",
            Monitoring, TaskManagement, "Details", "Assign", GrantedToManager: true),
        new(PermissionKeys.TasksCommentCreate, "Post task comments", "Write comments in a task's discussion.",
            Monitoring, TaskManagement, "Discussion", "Create", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.TasksCommentEdit, "Edit own task comments", "Change the text of a comment you wrote in a task's discussion.",
            Monitoring, TaskManagement, "Discussion", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.TasksCommentDelete, "Delete own task comments", "Delete a comment you wrote in a task's discussion.",
            Monitoring, TaskManagement, "Discussion", "Delete", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.TasksActivityView, "View task history", "Open a task's activity history.",
            Monitoring, TaskManagement, "Activity", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- TaskFlow Monitoring > Employee Workload -------------------------------------------------------------
        new(PermissionKeys.WorkloadView, "View workload", "Open the team workload view.",
            Monitoring, EmployeeWorkload, "", "View", GrantedToManager: true),
        new(PermissionKeys.WorkloadFilters, "Use workload filters", "Filter the workload table by project and employee.",
            Monitoring, EmployeeWorkload, "", "Use", GrantedToManager: true, GrantFrom: PermissionKeys.WorkloadView, Requires: PermissionKeys.WorkloadView),
        new(PermissionKeys.WorkloadTasksOpen, "Open an employee's tasks", "Follow the link from a workload row to that employee's task list. Task visibility still follows the user's task scope.",
            Monitoring, EmployeeWorkload, "", "Access", GrantedToManager: true, GrantFrom: PermissionKeys.WorkloadView, Requires: PermissionKeys.WorkloadView),

        // ---- TaskFlow Monitoring > Board -------------------------------------------------------------------------
        new(PermissionKeys.BoardView, "View board", "Open the task board and see the tasks the user's task scope allows.",
            Monitoring, Board, "", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.BoardFilters, "Use board filters", "Filter the board by project, assignee, status and deadline.",
            Monitoring, Board, "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true, Requires: PermissionKeys.BoardView),
        new(PermissionKeys.TasksStatus, "Update task status", "Move a task between statuses, on the board or the details screen.",
            Monitoring, Board, "", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- TaskFlow Management > Employee Management -----------------------------------------------------------
        new(PermissionKeys.EmployeesView, "View employees", "See the employee list and employee details.",
            Management, EmployeeManagement, "Employee list", "View", GrantedToManager: true),
        new(PermissionKeys.EmployeesCreate, "Create employees", "Add an employee.",
            Management, EmployeeManagement, "Employee list", "Create", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesEdit, "Edit employees", "Change an employee's details.",
            Management, EmployeeManagement, "Employee list", "Edit", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesStatus, "Activate or deactivate employees", "Switch an employee account on or off.",
            Management, EmployeeManagement, "Employee list", "Edit", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesRoleAssign, "Assign roles", "Change which role an employee holds.",
            Management, EmployeeManagement, "Employee list", "Assign", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesMfaReset, "Reset two-step verification", "Reset an employee's authenticator setup.",
            Management, EmployeeManagement, "Employee list", "Manage", Requires: PermissionKeys.EmployeesView),
        new(PermissionKeys.EmployeesDelete, "Delete employees", "Permanently remove an employee account.",
            Management, EmployeeManagement, "Employee list", "Delete", Requires: PermissionKeys.EmployeesView),

        // ---- TaskFlow Management > Project Management ------------------------------------------------------------
        new(PermissionKeys.ProjectsView, "View projects", "See projects, their members and their tasks.",
            Management, ProjectManagement, "Project list", "View", GrantedToManager: true),
        new(PermissionKeys.ProjectsCreate, "Create projects", "Create a project.",
            Management, ProjectManagement, "Project list", "Create", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsEdit, "Edit projects", "Rename a project and change its details.",
            Management, ProjectManagement, "Details", "Edit", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsDelete, "Delete projects", "Delete a project.",
            Management, ProjectManagement, "Project list", "Delete", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.ProjectsMembers, "Manage project members", "Add and remove the people in a project.",
            Management, ProjectManagement, "Members", "Assign", Requires: PermissionKeys.ProjectsView),
        new(PermissionKeys.PositionsCreate, "Create positions", "Add a job position used when allocating people to projects.",
            Management, ProjectManagement, "Members", "Create", Requires: PermissionKeys.ProjectsView),

        // ---- TaskFlow Management > Department Management ---------------------------------------------------------
        new(PermissionKeys.DepartmentsView, "View departments", "See departments and the managers and employees in them.",
            Management, DepartmentManagement, "Department list", "View", GrantedToManager: true),
        new(PermissionKeys.DepartmentsCreate, "Create departments", "Create a department.",
            Management, DepartmentManagement, "Department list", "Create", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsEdit, "Edit departments", "Rename a department and change its details.",
            Management, DepartmentManagement, "Department list", "Edit", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsDelete, "Delete departments", "Delete a department.",
            Management, DepartmentManagement, "Department list", "Delete", Requires: PermissionKeys.DepartmentsView),
        new(PermissionKeys.DepartmentsMembers, "Manage department members", "Choose the managers and employees in a department.",
            Management, DepartmentManagement, "Members", "Assign", Requires: PermissionKeys.DepartmentsView),

        // ---- Communication Management > Mail ---------------------------------------------------------------------
        new(PermissionKeys.MailRead, "Read mail", "Open the inbox, folders and messages.", Communication, "Mail", "Mailbox", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailCompose, "Write and send mail", "Compose, save drafts, reply and forward.", Communication, "Mail", "Mailbox", "Create", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailOrganize, "Organize mail", "Star, mark important, move to trash, restore and delete.", Communication, "Mail", "Mailbox", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.MailAttachments, "Use mail attachments", "Upload and download attachments.", Communication, "Mail", "Mailbox", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Communication Management > Notification -------------------------------------------------------------
        new(PermissionKeys.NotificationsView, "View notifications", "Open the notification centre.", Communication, "Notification", "Notification centre", "View", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.NotificationsManage, "Mark notifications read", "Mark one or all notifications as read.", Communication, "Notification", "Notification centre", "Edit", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),

        // ---- Report ----------------------------------------------------------------------------------------------
        new(PermissionKeys.ReportsView, "View reports", "Open task reports and filter them.",
            Report, "Reports", "", "View", GrantedToManager: true),
        new(PermissionKeys.ReportsExport, "Export reports", "Download a report as a file.",
            Report, "Reports", "", "Export", GrantedToManager: true, GrantFrom: PermissionKeys.ReportsView, Requires: PermissionKeys.ReportsView),

        // ---- Settings --------------------------------------------------------------------------------------------
        new(PermissionKeys.SettingsReminders, "Reminder settings", "View and change deadline reminder settings.",
            Settings, "Reminder Settings", "", "Manage", GrantedToManager: true),
        new(PermissionKeys.SettingsApplication, "Application settings", "Change application-wide settings.",
            Settings, "Application Settings", "", "Manage"),
        new(PermissionKeys.SettingsAiView, "View AI provider settings", "See which AI providers and models are configured.",
            Settings, "TFA Settings", "AI providers", "View"),
        new(PermissionKeys.SettingsAiEdit, "Edit AI provider settings", "Configure AI providers, keys and models.",
            Settings, "TFA Settings", "AI providers", "Edit", Requires: PermissionKeys.SettingsAiView),
        new(PermissionKeys.SettingsAiTest, "Test AI providers", "Run a connection test against a provider.",
            Settings, "TFA Settings", "AI providers", "Use", Requires: PermissionKeys.SettingsAiView),
        new(PermissionKeys.SettingsAiRemoveModel, "Remove AI models", "Remove a configured model from a provider.",
            Settings, "TFA Settings", "AI providers", "Delete", Requires: PermissionKeys.SettingsAiView),
        new(PermissionKeys.RolesManage, "Manage roles and permissions", "Open Role Configuration and create, edit and delete custom roles. Whoever holds it can also tick or untick it, and Assign roles, on custom roles.",
            Settings, "Role Configuration", "", "Manage"),
        new(PermissionKeys.RolesViewAll, "View all roles", "If ticked, the logged user can see all roles. If unticked, they can only see their own permissions.",
            Settings, "Role Configuration", "", "View"),
        new(PermissionKeys.RolesModifyAccess, "Modify access", "If ticked, the logged user can add or update permissions. If unticked, all permissions are in view mode.",
            Settings, "Role Configuration", "", "Edit"),
        new(PermissionKeys.RolesAdministratorAssign, "Give the Administrator role", "Make an account an Administrator, or take the role away. Only the Administrator role holds it.",
            Settings, "Role Configuration", "", "Assign", IsAssignable: false),

        // ---- My Profile (always on: sign-in and first-login set-up depend on it) ---------------------------------
        new(PermissionKeys.ProfileView, "View my profile", "Open the user's own profile.", Profile, "My profile", "Account", "View", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfileEdit, "Edit my personal information", "Update the user's own contact details.", Profile, "My profile", "Personal information", "Edit", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfilePassword, "Change my password", "Change the user's own password.", Profile, "My profile", "Security", "Edit", AlwaysGranted: true, IsAssignable: false),
        new(PermissionKeys.ProfileMfa, "Manage my authenticator", "Set up two-step verification and regenerate backup codes.", Profile, "My profile", "Security", "Manage", AlwaysGranted: true, IsAssignable: false),

        // ---- TFA assistant ---------------------------------------------------------------------------------------
        new(PermissionKeys.AssistantChat, "Chat with the assistant", "Ask the AI assistant questions and start new conversations.", Assistant, "Assistant", "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true),
        new(PermissionKeys.AssistantActions, "Confirm assistant actions", "Approve or cancel actions the assistant proposes. The assistant can only do what the user's own permissions allow.", Assistant, "Assistant", "", "Use", GrantedToManager: true, GrantedToEmployee: true, GrantToAllExistingRoles: true)
    ];

    /// <summary>Entries stored as grants. Always-on entries are not stored; every user simply has them.</summary>
    public static IEnumerable<PermissionDefinition> Stored => All.Where(definition => !definition.AlwaysGranted);
}
