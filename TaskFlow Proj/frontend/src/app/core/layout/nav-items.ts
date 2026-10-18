import { UserRole } from '../auth/auth.models';
import { IconName } from './icon.component';

export interface NavItem {
  label: string;
  icon: IconName;
  /** Route to navigate to. When omitted the item is rendered as a disabled "Soon" placeholder. */
  route?: string;
  /** Shows a live count next to the label: `mail` is the unread Inbox count. */
  badge?: 'mail';
}

/**
 * Side navigation per role. New authenticated screens register as children of the shell route
 * in app.routes.ts and add their entry here.
 */
export const ROLE_NAVIGATION: Record<UserRole, NavItem[]> = {
  Administrator: [
    { label: 'Dashboard', icon: 'home', route: '/admin' },
    { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
    { label: 'Task Management', icon: 'tasks', route: '/tasks' },
    { label: 'Employee Workload', icon: 'gauge', route: '/workload' },
    { label: 'Reports', icon: 'chart', route: '/reports' },
    { label: 'Employee Management', icon: 'users', route: '/employees' },
    { label: 'Department Management', icon: 'building', route: '/departments' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' },
    { label: 'Reminder Settings', icon: 'clock', route: '/settings/reminders' },
    { label: 'Application Settings', icon: 'settings', route: '/settings/application' },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ],
  Manager: [
    { label: 'Dashboard', icon: 'home', route: '/manager' },
    { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
    { label: 'Task Management', icon: 'tasks', route: '/tasks' },
    { label: 'Employee Workload', icon: 'gauge', route: '/workload' },
    { label: 'Reports', icon: 'chart', route: '/reports' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' },
    { label: 'Reminder Settings', icon: 'clock', route: '/settings/reminders' },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ],
  Employee: [
    { label: 'Dashboard', icon: 'home', route: '/me' },
    { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
    { label: 'My Tasks', icon: 'tasks', route: '/tasks' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ]
};
