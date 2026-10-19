/** The permission keys the SPA checks. They mirror the API's fixed permission catalog. */
export const Permissions = {
  DashboardAdmin: 'dashboard.admin',
  DashboardManager: 'dashboard.manager',
  DepartmentsView: 'departments.view',
  DepartmentsManage: 'departments.manage',
  EmployeesView: 'employees.view',
  EmployeesManage: 'employees.manage',
  TasksViewAll: 'tasks.view.all',
  TasksCreate: 'tasks.create',
  TasksUpdate: 'tasks.update',
  TasksReassign: 'tasks.reassign',
  TasksRecipients: 'tasks.recipients.manage',
  ReportsView: 'reports.view',
  WorkloadView: 'workload.view',
  SettingsReminders: 'settings.reminders.manage',
  SettingsApplication: 'settings.application.manage',
  SettingsAi: 'settings.ai.manage',
  RolesManage: 'roles.manage'
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];
