import { UserRole } from './auth.models';

/** Where each system role lands after sign-in. */
export const ROLE_ROUTES: Record<string, string> = {
  Administrator: '/admin',
  Manager: '/manager'
};

/** Every other role, including custom roles, lands on the personal dashboard. */
export const DEFAULT_ROUTE = '/me';

export function dashboardRouteFor(role: UserRole): string {
  return ROLE_ROUTES[role] ?? DEFAULT_ROUTE;
}
