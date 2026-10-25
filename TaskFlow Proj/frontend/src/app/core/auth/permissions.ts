/**
 * The permission keys the SPA checks. They mirror the stored entries of the API's module-based catalogue
 * (`PermissionCatalog.Stored`); a backend test fails when the two lists differ. Features every signed-in user has
 * (own profile, own dashboard and own tasks) need no key. Mail, notifications and the assistant do have keys; they are
 * given to every role when first added, so they can still be switched off for a role.
 */
export const Permissions = {
  // Dashboard: view team < view organization (each includes the lower level)
  DashboardViewTeam: 'dashboard.view.team',
  DashboardViewOrganization: 'dashboard.view.organization',
  DashboardAnalytics: 'dashboard.analytics.view',
  DashboardFilters: 'dashboard.filters.use',

  // Tasks
  TasksViewAll: 'tasks.view.all',
  TasksCreate: 'tasks.create',
  TasksDelete: 'tasks.delete',
  TasksStatus: 'tasks.status.change',
  TasksUpdate: 'tasks.update',
  TasksReassign: 'tasks.reassign',
  TasksRecipients: 'tasks.recipients.manage',
  TasksCommentCreate: 'tasks.comment.create',
  TasksCommentEdit: 'tasks.comment.edit',
  TasksCommentDelete: 'tasks.comment.delete',
  TasksActivityView: 'tasks.activity.view',

  // Projects
  ProjectsView: 'projects.view',
  ProjectsCreate: 'projects.create',
  ProjectsEdit: 'projects.edit',
  ProjectsDelete: 'projects.delete',
  ProjectsMembers: 'projects.members.manage',
  PositionsCreate: 'positions.create',

  // Departments
  DepartmentsView: 'departments.view',
  DepartmentsCreate: 'departments.create',
  DepartmentsEdit: 'departments.edit',
  DepartmentsDelete: 'departments.delete',
  DepartmentsMembers: 'departments.members.manage',

  // Employees
  EmployeesView: 'employees.view',
  EmployeesCreate: 'employees.create',
  EmployeesEdit: 'employees.edit',
  EmployeesStatus: 'employees.status.change',
  EmployeesDelete: 'employees.delete',
  EmployeesRoleAssign: 'employees.role.assign',
  EmployeesMfaReset: 'employees.mfa.reset',

  // Reports and workload
  ReportsView: 'reports.view',
  ReportsExport: 'reports.export',
  WorkloadView: 'workload.view',
  WorkloadFilters: 'workload.filters.use',
  WorkloadTasksOpen: 'workload.tasks.open',

  // Board
  BoardView: 'board.view',
  BoardFilters: 'board.filters.use',

  // Settings
  SettingsReminders: 'settings.reminders.manage',
  SettingsApplication: 'settings.application.manage',
  SettingsAiView: 'settings.ai.view',
  SettingsAiEdit: 'settings.ai.edit',
  SettingsAiTest: 'settings.ai.test',
  SettingsAiRemoveModel: 'settings.ai.model.remove',

  // Mail, notifications and the assistant
  MailRead: 'mail.read',
  MailCompose: 'mail.compose',
  MailOrganize: 'mail.organize',
  MailAttachments: 'mail.attachments',
  NotificationsView: 'notifications.view',
  NotificationsManage: 'notifications.manage',
  AssistantChat: 'assistant.chat',
  AssistantActions: 'assistant.actions.confirm',

  // Role configuration
  RolesManage: 'roles.manage',
  RolesViewAll: 'roles.view.all',
  RolesModifyAccess: 'roles.modify.access',
  RolesAdministratorAssign: 'roles.administrator.assign'
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];

/** Opening Employee Management or Department Management needs at least one of the actions on that screen. */
export const EmployeeScreenPermissions: PermissionKey[] = [
  Permissions.EmployeesCreate,
  Permissions.EmployeesEdit,
  Permissions.EmployeesStatus,
  Permissions.EmployeesDelete,
  Permissions.EmployeesRoleAssign,
  Permissions.EmployeesMfaReset
];

export const DepartmentScreenPermissions: PermissionKey[] = [
  Permissions.DepartmentsCreate,
  Permissions.DepartmentsEdit,
  Permissions.DepartmentsDelete,
  Permissions.DepartmentsMembers
];
