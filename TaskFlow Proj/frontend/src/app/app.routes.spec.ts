import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guard';
import { routes } from './app.routes';
import { AdminDashboardComponent } from './features/dashboard/admin-dashboard.component';
import { EmployeeDashboardComponent } from './features/dashboard/employee-dashboard.component';
import { ManagerDashboardComponent } from './features/dashboard/manager-dashboard.component';

describe('app routes', () => {
  function routeFor(path: string): Routes[number] {
    const route = routes.find((candidate) => candidate.path === path);
    expect(route).withContext(`route '${path}' should exist`).toBeDefined();
    return route!;
  }

  async function loadComponentFor(path: string): Promise<unknown> {
    const loadComponent = routeFor(path).loadComponent as () => Promise<unknown>;
    return loadComponent();
  }

  it('redirects the empty path to the login screen', () => {
    const root = routeFor('');
    expect(root.redirectTo).toBe('login');
    expect(root.pathMatch).toBe('full');
  });

  it('loads the login component without authentication guards', () => {
    const login = routeFor('login');
    expect(login.loadComponent).toBeDefined();
    expect(login.canActivate).toBeUndefined();
  });

  it('protects the administrator dashboard for Administrators only', () => {
    const admin = routeFor('admin');
    expect(admin.canActivate).toEqual([authGuard, roleGuard]);
    expect(admin.data?.['roles']).toEqual(['Administrator']);
  });

  it('protects the manager dashboard for Managers only', () => {
    const manager = routeFor('manager');
    expect(manager.canActivate).toEqual([authGuard, roleGuard]);
    expect(manager.data?.['roles']).toEqual(['Manager']);
  });

  it('protects the employee dashboard at /me for Employees only', () => {
    const me = routeFor('me');
    expect(me.canActivate).toEqual([authGuard, roleGuard]);
    expect(me.data?.['roles']).toEqual(['Employee']);
  });

  it('loads the role-specific dashboard components', async () => {
    expect(await loadComponentFor('admin')).toBe(AdminDashboardComponent);
    expect(await loadComponentFor('manager')).toBe(ManagerDashboardComponent);
    expect(await loadComponentFor('me')).toBe(EmployeeDashboardComponent);
  });
});
