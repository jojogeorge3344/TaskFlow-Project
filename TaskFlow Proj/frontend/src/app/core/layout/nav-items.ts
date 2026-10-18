import { UserRole } from '../auth/auth.models';
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
 * Side navigation per role. New authenticated screens register as children of the shell route
 * in app.routes.ts and add their entry here.
 */
export const ROLE_NAVIGATION: Record<UserRole, NavItem[]> = {
  Administrator: [
    { label: 'Dashboard', icon: 'home', route: '/admin' },
    {
      label: 'TaskFlow Monitoring',
      icon: 'tasks',
      children: [
        { label: 'Task Management', icon: 'tasks', route: '/tasks' },
        { label: 'Employee Workload', icon: 'gauge', route: '/workload' },
        { label: 'Board', icon: 'kanban', route: '/board' }
      ]
    },
    {
      label: 'TaskFlow Management',
      icon: 'tasks',
      children: [
        { label: 'Employee Management', icon: 'users', route: '/employees' },
        { label: 'Department Management', icon: 'building', route: '/departments' }
      ]
    },
    {
      label: 'Communication Management',
      icon: 'mail',
      children: [
        { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
        { label: 'Notifications', icon: 'bell', route: '/notifications' }
      ]
    },
    { label: 'Reports', icon: 'chart', route: '/reports' },
    {
      label: 'Settings',
      icon: 'settings',
      children: [
        { label: 'Reminder Settings', icon: 'clock', route: '/settings/reminders' },
        { label: 'Application Settings', icon: 'settings', route: '/settings/application' }
      ]
    },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ],
  Manager: [
    { label: 'Dashboard', icon: 'home', route: '/manager' },
    {
      label: 'TaskFlow Monitoring',
      icon: 'tasks',
      children: [
        { label: 'Task Management', icon: 'tasks', route: '/tasks' },
        { label: 'Employee Workload', icon: 'gauge', route: '/workload' },
        { label: 'Board', icon: 'kanban', route: '/board' }
      ]
    },
    {
      label: 'Communication Management',
      icon: 'mail',
      children: [
        { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
        { label: 'Notifications', icon: 'bell', route: '/notifications' }
      ]
    },
    { label: 'Reports', icon: 'chart', route: '/reports' },
    {
      label: 'Settings',
      icon: 'settings',
      children: [{ label: 'Reminder Settings', icon: 'clock', route: '/settings/reminders' }]
    },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ],
  Employee: [
    { label: 'Dashboard', icon: 'home', route: '/me' },
    {
      label: 'Communication Management',
      icon: 'mail',
      children: [
        { label: 'Mail', icon: 'mail', route: '/mail', badge: 'mail' },
        { label: 'Notifications', icon: 'bell', route: '/notifications' }
      ]
    },
    {
      label: 'Task Management',
      icon: 'tasks',
      children: [
        { label: 'My Tasks', icon: 'tasks', route: '/tasks' },
        { label: 'Board', icon: 'kanban', route: '/board' }
      ]
    },
    { label: 'My Profile', icon: 'user', route: '/profile' }
  ]
};
