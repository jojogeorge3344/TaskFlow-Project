import { UserRole } from './auth.models';

export const ROLE_ROUTES: Record<UserRole, string> = {
  Administrator: '/admin',
  Manager: '/manager',
  Employee: '/me'
};

export function dashboardRouteFor(role: UserRole): string {
  return ROLE_ROUTES[role];
}
