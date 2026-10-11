import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { AuthUser, UserRole } from './auth.models';
import { AuthService } from './auth.service';
import { authGuard, roleGuard } from './auth.guard';

describe('auth guards', () => {
  const userSignal = signal<AuthUser | null>(null);

  beforeEach(() => {
    userSignal.set(null);

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => userSignal() } }]
    });
  });

  function routeWithRoles(roles: UserRole[]) {
    return { data: { roles } } as unknown as Parameters<typeof roleGuard>[0];
  }

  function asUser(role: UserRole): void {
    userSignal.set({
      id: 1,
      fullName: 'Test User',
      username: 'test',
      email: 'test@taskflow.local',
      role,
      employeeCode: null
    });
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

  it('roleGuard allows a user whose role is permitted', () => {
    asUser('Administrator');

    const result = TestBed.runInInjectionContext(() => roleGuard(routeWithRoles(['Administrator']), {} as never));

    expect(result).toBeTrue();
  });

  it('roleGuard redirects a mismatched role to its own dashboard', () => {
    asUser('Manager');

    const result = TestBed.runInInjectionContext(() => roleGuard(routeWithRoles(['Administrator']), {} as never));

    expect((result as UrlTree).toString()).toBe('/manager');
  });

  it('roleGuard redirects unauthenticated users to /login', () => {
    const result = TestBed.runInInjectionContext(() => roleGuard(routeWithRoles(['Administrator']), {} as never));

    expect((result as UrlTree).toString()).toBe('/login');
  });

  it('router can be injected alongside the guards', () => {
    expect(TestBed.inject(Router)).toBeTruthy();
  });
});
