import { Permissions } from '../core/auth/permissions';

/** The permissions each system role holds, as the API seeds them. For specs only. */
const MANAGER_PERMISSIONS: string[] = [
  Permissions.DashboardViewTeam,
  Permissions.DashboardAnalytics,
  Permissions.DashboardFilters,
  Permissions.TasksStatus,
  Permissions.TasksCommentCreate,
  Permissions.TasksActivityView,
  Permissions.ReportsExport,
  Permissions.MailRead,
  Permissions.MailCompose,
  Permissions.MailOrganize,
  Permissions.MailAttachments,
  Permissions.NotificationsView,
  Permissions.NotificationsManage,
  Permissions.AssistantChat,
  Permissions.AssistantActions,
  Permissions.ProjectsView,
  Permissions.DepartmentsView,
  Permissions.EmployeesView,
  Permissions.TasksViewAll,
  Permissions.TasksCreate,
  Permissions.TasksUpdate,
  Permissions.TasksReassign,
  Permissions.TasksRecipients,
  Permissions.ReportsView,
  Permissions.WorkloadView,
  Permissions.WorkloadFilters,
  Permissions.WorkloadTasksOpen,
  Permissions.BoardView,
  Permissions.BoardFilters,
  Permissions.SettingsReminders
];

export const ADMINISTRATOR_PERMISSIONS: string[] = Object.values(Permissions);

export function systemPermissions(role: string): string[] {
  switch (role) {
    case 'Administrator':
      return [...ADMINISTRATOR_PERMISSIONS];
    case 'Manager':
      return [...MANAGER_PERMISSIONS];
    default:
      return [
        Permissions.DashboardAnalytics,
        Permissions.DashboardFilters,
        Permissions.TasksStatus,
        Permissions.TasksCommentCreate,
        Permissions.TasksActivityView,
        Permissions.BoardView,
        Permissions.BoardFilters,
        Permissions.MailRead,
        Permissions.MailCompose,
        Permissions.MailOrganize,
        Permissions.MailAttachments,
        Permissions.NotificationsView,
        Permissions.NotificationsManage,
        Permissions.AssistantChat,
        Permissions.AssistantActions
      ];
  }
}

export function systemRoleId(role: string): number {
  return role === 'Administrator' ? 1 : role === 'Manager' ? 2 : 3;
}

/** A stand-in for AuthService in component specs: the signed-in user plus a working hasPermission. */
export function fakeAuthService(
  user: () => { permissions: string[] } | null,
  extra: Record<string, unknown> = {}
): Record<string, unknown> {
  return {
    currentUser: user,
    hasPermission: (key: string) => user()?.permissions.includes(key) ?? false,
    ...extra
  };
}
