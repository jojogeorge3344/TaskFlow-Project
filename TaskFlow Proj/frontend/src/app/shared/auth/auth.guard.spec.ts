import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { systemPermissions } from '../testing/permissions';
import { AuthUser } from './auth.models';
import { AuthService } from './auth.service';
import { authGuard, permissionGuard } from './auth.guard';
import { Permissions } from './permissions';

describe('auth guards', () => {
  const userSignal = signal<AuthUser | null>(null);

  beforeEach(() => {
    userSignal.set(null);

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => userSignal() } }]
    });
  });

  function routeNeeding(...permissions: string[]) {
    return { data: { permissions } } as unknown as Parameters<typeof permissionGuard>[0];
  }

  function asUser(role: string, permissions: string[] = systemPermissions(role)): void {
    userSignal.set({
      id: 1,
      fullName: 'Test User',
      username: 'test',
      email: 'test@taskflow.com',
      role,
      employeeCode: null,
      permissions
    });
  }

  function guard(...permissions: string[]) {
    return TestBed.runInInjectionContext(() => permissionGuard(routeNeeding(...permissions), {} as never));
  }

  it('authGuard allows an authenticated user', () => {
    asUser('Administrator');

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('authGuard redirects unauthenticated users to /login', () => {
    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect((result as UrlTree).toString()).toBe('/login');
  });

  it('permissionGuard allows a user whose role holds the permission', () => {
    asUser('Administrator');

    expect(guard(Permissions.RolesManage)).toBeTrue();
  });

  it('permissionGuard allows a user holding any one of several permissions', () => {
    asUser('Manager');

    expect(guard(Permissions.RolesManage, Permissions.ReportsView)).toBeTrue();
  });

  it('permissionGuard sends a system role without the permission to its own dashboard', () => {
    asUser('Manager');

    expect((guard(Permissions.RolesManage) as UrlTree).toString()).toBe('/manager');
  });

  it('permissionGuard sends an Employee to /me', () => {
    asUser('Employee');

    expect((guard(Permissions.ReportsView) as UrlTree).toString()).toBe('/me');
  });

  it('permissionGuard sends a custom role without the permission to /me', () => {
    asUser('Team Lead', [Permissions.TasksCreate]);

    expect(guard(Permissions.TasksCreate)).toBeTrue();
    expect((guard(Permissions.ReportsView) as UrlTree).toString()).toBe('/me');
  });

  it('permissionGuard refuses a route that names no permission', () => {
    asUser('Administrator');

    expect((guard() as UrlTree).toString()).toBe('/admin');
  });

  it('permissionGuard redirects unauthenticated users to /login', () => {
    expect((guard(Permissions.ReportsView) as UrlTree).toString()).toBe('/login');
  });

  it('router can be injected alongside the guards', () => {
    expect(TestBed.inject(Router)).toBeTruthy();
  });
});
