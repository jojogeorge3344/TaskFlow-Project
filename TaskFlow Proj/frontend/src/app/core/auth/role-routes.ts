import { UserRole } from './auth.models';

/** Where each system role lands after sign-in. */
export const ROLE_ROUTES: Record<string, string> = {
  Administrator: '/admin',
  Manager: '/manager'
};

/** Every other role, including custom roles, lands on the personal dashboard. */
export const DEFAULT_ROUTE = '/me';

/**
 * Where a user's dashboard lives. The system roles keep their own routes; a custom role goes to the widest dashboard
 * its permissions allow (organization, then team) and otherwise to the personal one.
 */
export function dashboardRouteFor(role: UserRole, permissions: readonly string[] = []): string {
  const fixed = ROLE_ROUTES[role];
  if (fixed) {
    return fixed;
  }

  if (permissions.includes('dashboard.view.organization')) {
    return '/admin';
  }

  return permissions.includes('dashboard.view.team') ? '/manager' : DEFAULT_ROUTE;
}
