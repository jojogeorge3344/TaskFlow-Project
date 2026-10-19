import { AuthUser } from '../auth/auth.models';
import { Permissions } from '../auth/permissions';
import { dashboardRouteFor } from '../auth/role-routes';
import { IconName } from './icon.component';

export interface NavItem {
  label: string;
  icon: IconName;
  /** Route to navigate to. When omitted the item is rendered as a disabled "Soon" placeholder. */
  route?: string;
  /** Shows a live count next to the label: `mail` is the unread Inbox count. */
  badge?: 'mail';
  /** Sub-module entries rendered beneath this item when present (a parent module). */
  children?: NavItem[];
}

/**
 * Side navigation for a signed-in user, built from the permissions their role holds, so a custom role sees exactly the
 * screens it may open. The three system roles get the same menus as before permissions existed. New authenticated
 * screens register as children of the shell route in app.routes.ts and add their entry here.
 */
export function buildNavigation(user: Pick<AuthUser, 'role' | 'permissions'> | null | undefined): NavItem[] {
  if (!user) {
    return [];
  }

  const has = (key: string) => user.permissions.includes(key);
  const canViewAllTasks = has(Permissions.TasksViewAll);
  const workload: NavItem[] = has(Permissions.WorkloadView)
    ? [{ label: 'Employee Workload', icon: 'gauge', route: '/workload' }]
    : [];

  const items: NavItem[] = [{ label: 'Dashboard', icon: 'home', route: dashboardRouteFor(user.role) }];

  if (canViewAllTasks) {
    items.push({
      label: 'TaskFlow Monitoring',
      icon: 'tasks',
      children: [
        { label: 'Task Management', icon: 'tasks', route: '/tasks' },
        ...workload,
        { label: 'Board', icon: 'kanban', route: '/board' }
      ]
    });
  }

  const management: NavItem[] = [];
  if (has(Permissions.EmployeesManage)) {
    management.push({ label: 'Employee Management', icon: 'users', route: '/employees' });
  }
  if (has(Permissions.DepartmentsManage)) {
    management.push({ label: 'Department Management', icon: 'building', route: '/departments' });
  }
  if (management.length > 0) {
    items.push({ label: 'TaskFlow Management', icon: 'tasks', children: management });
  }

  items.push({
    label: 'Communication Management',
    icon: 'mail',
    children: [
      { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
      { label: 'Notifications', icon: 'bell', route: '/notifications' }
    ]
  });

  if (!canViewAllTasks) {
    items.push({
      label: 'Task Management',
      icon: 'tasks',
      children: [{ label: 'My Tasks', icon: 'tasks', route: '/tasks' }, ...workload, { label: 'Board', icon: 'kanban', route: '/board' }]
    });
  }

  if (has(Permissions.ReportsView)) {
    items.push({ label: 'Reports', icon: 'chart', route: '/reports' });
  }

  const settings: NavItem[] = [];
  if (has(Permissions.SettingsReminders)) {
    settings.push({ label: 'Reminder Settings', icon: 'clock', route: '/settings/reminders' });
  }
  if (has(Permissions.SettingsApplication)) {
    settings.push({ label: 'Application Settings', icon: 'settings', route: '/settings/application' });
  }
  if (has(Permissions.RolesManage)) {
    settings.push({ label: 'Role Configuration', icon: 'shield', route: '/settings/permissions' });
  }
  if (settings.length > 0) {
    items.push({ label: 'Settings', icon: 'settings', children: settings });
  }

  items.push({ label: 'My Profile', icon: 'user', route: '/profile' });

  return items;
}
