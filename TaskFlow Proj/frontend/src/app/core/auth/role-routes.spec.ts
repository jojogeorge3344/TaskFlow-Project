import { UserRole } from './auth.models';
import { dashboardRouteFor, ROLE_ROUTES } from './role-routes';

describe('role routes', () => {
  it('maps every role to its dashboard route', () => {
    const expected: Record<UserRole, string> = {
      Administrator: '/admin',
      Manager: '/manager',
      Employee: '/me'
    };

    expect(ROLE_ROUTES).toEqual(expected);
  });

  it('resolves the dashboard route for each role', () => {
    expect(dashboardRouteFor('Administrator')).toBe('/admin');
    expect(dashboardRouteFor('Manager')).toBe('/manager');
    expect(dashboardRouteFor('Employee')).toBe('/me');
  });
});
