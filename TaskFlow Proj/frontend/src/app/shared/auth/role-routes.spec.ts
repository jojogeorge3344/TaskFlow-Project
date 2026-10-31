import { dashboardRouteFor, DEFAULT_ROUTE, ROLE_ROUTES } from './role-routes';

describe('role routes', () => {
  it('gives the Administrator and the Manager their own dashboards', () => {
    expect(ROLE_ROUTES).toEqual({ Administrator: '/admin', Manager: '/manager' });
  });

  it('resolves the dashboard route for each system role', () => {
    expect(dashboardRouteFor('Administrator')).toBe('/admin');
    expect(dashboardRouteFor('Manager')).toBe('/manager');
    expect(dashboardRouteFor('Employee')).toBe('/me');
  });

  it('sends every custom role to the personal dashboard', () => {
    expect(DEFAULT_ROUTE).toBe('/me');
    expect(dashboardRouteFor('Team Lead')).toBe('/me');
    expect(dashboardRouteFor('')).toBe('/me');
  });
});
