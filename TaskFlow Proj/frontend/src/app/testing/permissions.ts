import { Permissions } from '../core/auth/permissions';

/** The permissions each system role holds, as the API seeds them. For specs only. */
const MANAGER_PERMISSIONS: string[] = [
  Permissions.DashboardManager,
  Permissions.ProjectsView,
  Permissions.EmployeesView,
  Permissions.TasksViewAll,
  Permissions.TasksCreate,
  Permissions.TasksUpdate,
  Permissions.TasksReassign,
  Permissions.TasksRecipients,
  Permissions.ReportsView,
  Permissions.WorkloadView,
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
      return [];
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
