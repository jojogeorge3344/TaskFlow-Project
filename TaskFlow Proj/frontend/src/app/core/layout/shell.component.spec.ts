import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthUser, UserRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { SIDEBAR_COLLAPSED_KEY, WIDE_VIEWPORT_QUERY } from './layout.service';
import { SESSION_CHECK_INTERVAL_MS, ShellComponent } from './shell.component';

@Component({ standalone: true, template: '<p class="routed">Routed page</p>' })
class PageStubComponent {}

describe('ShellComponent', () => {
  const userSignal = signal<AuthUser | null>(null);
  const logoutSpy = jasmine.createSpy('logout');
  const checkSessionSpy = jasmine.createSpy('checkSession');

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

  function mockViewport(wide: boolean): void {
    spyOn(window, 'matchMedia').and.callFake(
      (query: string) =>
        ({
          matches: query === WIDE_VIEWPORT_QUERY ? wide : false,
          addEventListener: () => undefined
        }) as unknown as MediaQueryList
    );
  }

  function createComponent(): ComponentFixture<ShellComponent> {
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ShellComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function toggleButton(fixture: ComponentFixture<ShellComponent>): HTMLButtonElement {
    return el(fixture).querySelector('.header__toggle') as HTMLButtonElement;
  }

  beforeEach(async () => {
    localStorage.removeItem(SIDEBAR_COLLAPSED_KEY);
    userSignal.set(null);
    logoutSpy.calls.reset();
    logoutSpy.and.returnValue(of(undefined));
    checkSessionSpy.calls.reset();
    checkSessionSpy.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([
          { path: 'employees', component: PageStubComponent },
          { path: 'admin', component: PageStubComponent }
        ]),
        {
          provide: AuthService,
          useValue: { currentUser: userSignal.asReadonly(), logout: logoutSpy, checkSession: checkSessionSpy }
        }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    localStorage.removeItem(SIDEBAR_COLLAPSED_KEY);
  });

  it('shows the administrator navigation menu with icons and the enabled task entry', () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    const links = Array.from(el(fixture).querySelectorAll('a.nav__item'));
    const labels = links.map((link) => link.querySelector('.nav__text')?.textContent?.trim());

    expect(labels).toEqual(['Dashboard', 'Task Management', 'Employee Management', 'Department Management']);
    expect(links.every((link) => link.querySelector('svg.tf-icon') !== null)).toBeTrue();

    const taskLink = links.find((link) => link.querySelector('.nav__text')?.textContent?.trim() === 'Task Management')!;
    expect(taskLink.getAttribute('href')).toBe('/board');
    expect(el(fixture).querySelector('.nav__item--disabled')).toBeNull();
  });

  it('limits the employee navigation menu to their own links', () => {
    mockViewport(true);
    asUser('Employee');
    const fixture = createComponent();

    const links = Array.from(el(fixture).querySelectorAll('a.nav__item .nav__text')).map((node) =>
      node.textContent?.trim()
    );

    expect(links).toEqual(['Dashboard', 'My Tasks']);
    expect(el(fixture).querySelector('.nav__item--disabled')).toBeNull();
    expect(el(fixture).textContent).not.toContain('Employee Management');
  });

  it('highlights the active item with aria-current', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    await TestBed.inject(Router).navigateByUrl('/employees');
    fixture.detectChanges();

    const active = el(fixture).querySelector('a.nav__item--active')!;
    expect(active.querySelector('.nav__text')?.textContent?.trim()).toBe('Employee Management');
    expect(active.getAttribute('aria-current')).toBe('page');
    expect(el(fixture).querySelector('.header__title')?.textContent).toContain('Employee Management');
    expect(el(fixture).querySelector('.routed')).toBeTruthy();
  });

  it('collapses and expands the menu on wide screens and reports aria-expanded', () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();
    const shell = el(fixture).querySelector('.shell')!;

    expect(toggleButton(fixture).getAttribute('aria-expanded')).toBe('true');
    expect(toggleButton(fixture).getAttribute('aria-label')).toBe('Toggle navigation');

    toggleButton(fixture).click();
    fixture.detectChanges();

    expect(shell.classList).toContain('shell--collapsed');
    expect(toggleButton(fixture).getAttribute('aria-expanded')).toBe('false');
    expect(localStorage.getItem(SIDEBAR_COLLAPSED_KEY)).toBe('true');

    toggleButton(fixture).click();
    fixture.detectChanges();

    expect(shell.classList).not.toContain('shell--collapsed');
    expect(toggleButton(fixture).getAttribute('aria-expanded')).toBe('true');
  });

  it('keeps each label as the accessible name while collapsed', () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    toggleButton(fixture).click();
    fixture.detectChanges();

    const firstLink = el(fixture).querySelector('a.nav__item')!;
    expect(firstLink.querySelector('.nav__tip')?.textContent?.trim()).toBe('Dashboard');
  });

  it('opens the mobile drawer and closes it with the backdrop, returning focus to the toggle', () => {
    mockViewport(false);
    asUser('Administrator');
    const fixture = createComponent();

    expect(toggleButton(fixture).getAttribute('aria-expanded')).toBe('false');

    toggleButton(fixture).click();
    fixture.detectChanges();

    expect(el(fixture).querySelector('.shell')?.classList).toContain('shell--open');
    expect(toggleButton(fixture).getAttribute('aria-expanded')).toBe('true');

    (el(fixture).querySelector('.backdrop') as HTMLElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelector('.shell')?.classList).not.toContain('shell--open');
    expect(el(fixture).querySelector('.backdrop')).toBeNull();
    expect(document.activeElement).toBe(toggleButton(fixture));
  });

  it('closes the mobile drawer with Escape', () => {
    mockViewport(false);
    asUser('Administrator');
    const fixture = createComponent();

    toggleButton(fixture).click();
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(el(fixture).querySelector('.shell')?.classList).not.toContain('shell--open');
    expect(document.activeElement).toBe(toggleButton(fixture));
  });

  it('closes the mobile drawer after navigating', async () => {
    mockViewport(false);
    asUser('Administrator');
    const fixture = createComponent();

    toggleButton(fixture).click();
    fixture.detectChanges();

    await TestBed.inject(Router).navigateByUrl('/admin');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.shell')?.classList).not.toContain('shell--open');
  });

  it('shows the account initials, name and role', () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();

    expect(el(fixture).querySelector('.account__avatar')?.textContent?.trim()).toBe('TU');
    expect(el(fixture).querySelector('.account__name')?.textContent).toContain('Test User');
    expect(el(fixture).querySelector('.account__role')?.textContent).toContain('Manager');
  });

  it('re-checks the session every minute and when the tab becomes visible', fakeAsync(() => {
    mockViewport(true);
    asUser('Employee');
    createComponent();

    expect(checkSessionSpy).not.toHaveBeenCalled();

    tick(SESSION_CHECK_INTERVAL_MS);
    expect(checkSessionSpy).toHaveBeenCalledTimes(1);

    spyOnProperty(document, 'visibilityState').and.returnValue('visible');
    document.dispatchEvent(new Event('visibilitychange'));
    expect(checkSessionSpy).toHaveBeenCalledTimes(2);

    discardPeriodicTasks();
  }));

  it('signs out and returns to login', () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();
    const navigateSpy = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

    (el(fixture).querySelector('.account__signout') as HTMLButtonElement).click();

    expect(logoutSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
