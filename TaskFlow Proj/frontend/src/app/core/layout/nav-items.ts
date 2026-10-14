import { UserRole } from '../auth/auth.models';
import { IconName } from './icon.component';

export interface NavItem {
  label: string;
  icon: IconName;
  /** Route to navigate to. When omitted the item is rendered as a disabled "Soon" placeholder. */
  route?: string;
}

/**
 * Side navigation per role. New authenticated screens register as children of the shell route
 * in app.routes.ts and add their entry here.
 */
export const ROLE_NAVIGATION: Record<UserRole, NavItem[]> = {
  Administrator: [
    { label: 'Dashboard', icon: 'home', route: '/admin' },
    { label: 'Task Management', icon: 'tasks', route: '/tasks' },
    { label: 'Employee Management', icon: 'users', route: '/employees' },
    { label: 'Department Management', icon: 'building', route: '/departments' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' }
  ],
  Manager: [
    { label: 'Dashboard', icon: 'home', route: '/manager' },
    { label: 'Task Management', icon: 'tasks', route: '/tasks' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' }
  ],
  Employee: [
    { label: 'Dashboard', icon: 'home', route: '/me' },
    { label: 'My Tasks', icon: 'tasks', route: '/tasks' },
    { label: 'Notifications', icon: 'bell', route: '/notifications' }
  ]
};
