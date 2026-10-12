import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { AuthUser, UserRole } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardLayoutComponent } from './dashboard-layout.component';

describe('DashboardLayoutComponent', () => {
  const userSignal = signal<AuthUser | null>(null);
  const logoutSpy = jasmine.createSpy('logout');

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

  function createComponent(): ComponentFixture<DashboardLayoutComponent> {
    const fixture = TestBed.createComponent(DashboardLayoutComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    userSignal.set(null);
    logoutSpy.calls.reset();
    logoutSpy.and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [DashboardLayoutComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser: userSignal.asReadonly(), logout: logoutSpy } }
      ]
    }).compileComponents();
  });

  it('shows the administrator navigation menu', () => {
    asUser('Administrator');
    const fixture = createComponent();
    const element = fixture.nativeElement as HTMLElement;

    const labels = Array.from(element.querySelectorAll('.layout__link')).map((el) =>
      el.textContent?.replace(/\s+/g, ' ').trim()
    );

    expect(labels).toContain('Dashboard');
    expect(labels).toContain('Employee Management');
    expect(labels).toContain('Department Management');
    expect(labels.some((label) => label?.startsWith('Task Management'))).toBeTrue();
  });

  it('limits the employee navigation menu to their own links', () => {
    asUser('Employee');
    const fixture = createComponent();
    const element = fixture.nativeElement as HTMLElement;

    const links = Array.from(element.querySelectorAll('a.layout__link')).map((el) => el.textContent?.trim());

    expect(links).toEqual(['Dashboard']);
    expect(element.querySelector('.layout__link--disabled')?.textContent).toContain('My Tasks');
  });

  it('toggles the responsive sidebar', () => {
    asUser('Manager');
    const fixture = createComponent();
    const component = fixture.componentInstance;

    expect(component.sidebarOpen()).toBeFalse();
    component.toggleSidebar();
    expect(component.sidebarOpen()).toBeTrue();
    component.closeSidebar();
    expect(component.sidebarOpen()).toBeFalse();
  });

  it('signs out and returns to login', () => {
    asUser('Manager');
    const fixture = createComponent();
    const navigateSpy = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

    fixture.componentInstance.logout();

    expect(logoutSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
