import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { NavigationEnd, Router, provideRouter } from '@angular/router';
import { filter, firstValueFrom, of, take } from 'rxjs';
import { AuthUser, UserRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { MailService } from '../services/mail.service';
import { NotificationService } from '../services/notification.service';
import { ApplicationSettingsService } from '../services/application-settings.service';
import { SIDEBAR_COLLAPSED_KEY, WIDE_VIEWPORT_QUERY } from './layout.service';
import { SESSION_CHECK_INTERVAL_MS, ShellComponent } from './shell.component';

@Component({ standalone: true, template: '<p class="routed">Routed page</p>' })
class PageStubComponent {}

describe('ShellComponent', () => {
  const userSignal = signal<AuthUser | null>(null);
  const logoutSpy = jasmine.createSpy('logout');
  const checkSessionSpy = jasmine.createSpy('checkSession');
  const unreadCountSignal = signal(0);
  const refreshUnreadCountSpy = jasmine.createSpy('refreshUnreadCount');
  const mailUnreadSignal = signal(0);
  const refreshMailCountSpy = jasmine.createSpy('refreshMailCount');
  const getAppSettingsSpy = jasmine.createSpy('get');
  const appNameSignal = signal('TaskFlow');

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

  function parentButton(fixture: ComponentFixture<ShellComponent>, label: string): HTMLButtonElement {
    return Array.from(el(fixture).querySelectorAll('.nav__parent')).find(
      (node) => node.querySelector('.nav__text')?.textContent?.trim() === label
    ) as HTMLButtonElement;
  }

  function expandParent(fixture: ComponentFixture<ShellComponent>, label: string): void {
    const parent = parentButton(fixture, label);
    if (parent.getAttribute('aria-expanded') !== 'true') {
      parent.click();
      fixture.detectChanges();
    }
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
    unreadCountSignal.set(0);
    refreshUnreadCountSpy.calls.reset();
    mailUnreadSignal.set(0);
    refreshMailCountSpy.calls.reset();
    getAppSettingsSpy.calls.reset();
    getAppSettingsSpy.and.returnValue(
      of({ name: 'TaskFlow', timezone: 'UTC', defaultPriority: 'Medium', inAppNotificationsEnabled: true })
    );
    appNameSignal.set('TaskFlow');

    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([
          { path: 'employees', component: PageStubComponent },
          { path: 'admin', component: PageStubComponent },
          { path: 'notifications', component: PageStubComponent },
          { path: 'settings/reminders', component: PageStubComponent }
        ]),
        {
          provide: AuthService,
          useValue: { currentUser: userSignal.asReadonly(), logout: logoutSpy, checkSession: checkSessionSpy }
        },
        {
          provide: NotificationService,
          useValue: { unreadCount: unreadCountSignal.asReadonly(), refreshUnreadCount: refreshUnreadCountSpy }
        },
        {
          provide: MailService,
          useValue: { unreadCount: mailUnreadSignal.asReadonly(), refreshUnreadCount: refreshMailCountSpy }
        },
        {
          provide: ApplicationSettingsService,
          useValue: { get: getAppSettingsSpy, appName: appNameSignal.asReadonly() }
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
    ['TaskFlow Monitoring', 'TaskFlow Management', 'Communication Management', 'Settings'].forEach((label) =>
      expandParent(fixture, label)
    );

    const links = Array.from(el(fixture).querySelectorAll('a.nav__item'));
    const labels = links.map((link) => link.querySelector('.nav__text')?.textContent?.trim());

    expect(labels).toEqual([
      'Dashboard',
      'Task Management',
      'Employee Workload',
      'Board',
      'Employee Management',
      'Department Management',
      'Mail',
      'Notifications',
      'Reports',
      'Reminder Settings',
      'Application Settings',
      'My Profile'
    ]);
    expect(links.every((link) => link.querySelector('svg.tf-icon') !== null)).toBeTrue();

    const taskLink = links.find((link) => link.querySelector('.nav__text')?.textContent?.trim() === 'Task Management')!;
    expect(taskLink.getAttribute('href')).toBe('/tasks');
    expect(el(fixture).querySelector('.nav__item--disabled')).toBeNull();
  });

  it('starts all parent modules minimized and reveals children on expand', () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    const parents = Array.from(el(fixture).querySelectorAll('.nav__parent'));
    expect(parents.map((node) => node.querySelector('.nav__text')?.textContent?.trim())).toEqual([
      'TaskFlow Monitoring',
      'TaskFlow Management',
      'Communication Management',
      'Settings'
    ]);
    expect(parents.every((node) => node.getAttribute('aria-expanded') === 'false')).toBeTrue();
    expect(el(fixture).querySelector('.nav__item--child')).toBeNull();

    const groups = Array.from(el(fixture).querySelectorAll('.nav__group'));
    const childrenOf = (index: number) => {
      (parents[index] as HTMLButtonElement).click();
      fixture.detectChanges();
      return Array.from(groups[index].querySelectorAll('.nav__item--child .nav__text')).map(
        (node) => node.textContent?.trim()
      );
    };
    expect(childrenOf(0)).toEqual(['Task Management', 'Employee Workload', 'Board']);
    expect(childrenOf(1)).toEqual(['Employee Management', 'Department Management']);
    expect(childrenOf(2)).toEqual(['Mail', 'Notifications']);
    expect(childrenOf(3)).toEqual(['Reminder Settings', 'Application Settings']);
  });

  it('collapses and expands a parent module group', () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    const settings = parentButton(fixture, 'Settings');
    expect(settings.getAttribute('aria-controls')).toBe('nav-group-settings');
    expect(settings.getAttribute('aria-expanded')).toBe('false');
    expect(el(fixture).querySelector('#nav-group-settings')).toBeNull();

    settings.click();
    fixture.detectChanges();

    expect(settings.getAttribute('aria-expanded')).toBe('true');
    expect(el(fixture).querySelector('#nav-group-settings')).not.toBeNull();

    settings.click();
    fixture.detectChanges();

    expect(settings.getAttribute('aria-expanded')).toBe('false');
    expect(el(fixture).querySelector('#nav-group-settings')).toBeNull();
  });

  it('wraps long parent labels instead of truncating them', () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    const parentLabel = Array.from(el(fixture).querySelectorAll('.nav__parent .nav__text')).find(
      (node) => node.textContent?.trim() === 'Communication Management'
    )!;
    const style = getComputedStyle(parentLabel);
    expect(style.whiteSpace).toBe('normal');
    expect(style.textOverflow).toBe('clip');

    expandParent(fixture, 'TaskFlow Monitoring');
    const childLabel = el(fixture).querySelector('.nav__item--child .nav__text')!;
    expect(getComputedStyle(childLabel).whiteSpace).toBe('nowrap');
  });

  it('collapses a parent group even while a child route is active', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();
    await TestBed.inject(Router).navigateByUrl('/settings/reminders');
    fixture.detectChanges();

    const settings = parentButton(fixture, 'Settings');
    expect(settings.getAttribute('aria-expanded')).toBe('true');

    settings.click();
    fixture.detectChanges();

    expect(settings.getAttribute('aria-expanded')).toBe('false');
    expect(el(fixture).querySelector('#nav-group-settings')).toBeNull();
  });

  it('re-expands a manually collapsed group on the next navigation', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/settings/reminders');
    fixture.detectChanges();

    const settings = () => parentButton(fixture, 'Settings');
    settings().click();
    fixture.detectChanges();
    expect(settings().getAttribute('aria-expanded')).toBe('false');

    await router.navigateByUrl('/employees');
    fixture.detectChanges();
    expect(settings().getAttribute('aria-expanded')).toBe('false');

    await router.navigateByUrl('/settings/reminders');
    fixture.detectChanges();
    expect(settings().getAttribute('aria-expanded')).toBe('true');
    expect(el(fixture).querySelector('#nav-group-settings')).not.toBeNull();
  });

  it('navigates to a sub-module when its child entry is clicked', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();
    const router = TestBed.inject(Router);
    const arrived = firstValueFrom(
      router.events.pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        take(1)
      )
    );

    expandParent(fixture, 'TaskFlow Management');
    const child = Array.from(el(fixture).querySelectorAll('.nav__item--child')).find(
      (node) => node.querySelector('.nav__text')?.textContent?.trim() === 'Employee Management'
    ) as HTMLAnchorElement;

    child.click();
    fixture.detectChanges();
    await arrived;
    // RouterLinkActive applies the active class inside a queued microtask; flush it.
    await new Promise<void>((resolve) => setTimeout(resolve));
    fixture.detectChanges();

    expect(router.url).toBe('/employees');
    expect(el(fixture).querySelector('.routed')).toBeTruthy();
    expect(child.classList).toContain('nav__item--active');
    expect(child.getAttribute('aria-current')).toBe('page');
    expect(el(fixture).querySelector('.header__title')?.textContent).toContain('Employee Management');
  });

  it('shows the sub-module label in the header title for a grouped route', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    await TestBed.inject(Router).navigateByUrl('/settings/reminders');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.header__title')?.textContent).toContain('Reminder Settings');
  });

  it('limits the employee navigation menu to their own links', () => {
    mockViewport(true);
    asUser('Employee');
    const fixture = createComponent();

    expect(Array.from(el(fixture).querySelectorAll('a.nav__item .nav__text')).map((node) => node.textContent?.trim()))
      .toEqual(['Dashboard', 'My Profile']);

    expandParent(fixture, 'Communication Management');
    expandParent(fixture, 'Task Management');

    const links = Array.from(el(fixture).querySelectorAll('a.nav__item .nav__text')).map((node) =>
      node.textContent?.trim()
    );

    expect(links).toEqual(['Dashboard', 'Mail', 'Notifications', 'My Tasks', 'Board', 'My Profile']);
    expect(el(fixture).querySelector('.nav__item--disabled')).toBeNull();
    expect(el(fixture).textContent).not.toContain('Employee Management');
    expect(el(fixture).textContent).not.toContain('Reminder Settings');
  });

  it('highlights the active item with aria-current', async () => {
    mockViewport(true);
    asUser('Administrator');
    const fixture = createComponent();

    await TestBed.inject(Router).navigateByUrl('/employees');
    // The group expands after navigation, so the child links (and their active state)
    // are created in the following change detection; flush RouterLinkActive's microtask.
    fixture.detectChanges();
    await new Promise<void>((resolve) => setTimeout(resolve));
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

  it('shows the brand name from the settings service and refreshes it when the service updates', () => {
    mockViewport(true);
    asUser('Manager');
    appNameSignal.set('Acme Tasks');
    const fixture = createComponent();

    expect(el(fixture).querySelector('.brand__name')?.textContent?.trim()).toBe('Acme Tasks');
    expect(el(fixture).querySelector('.header__brand')?.textContent?.trim()).toBe('Acme Tasks');

    appNameSignal.set('Renamed Corp');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.brand__name')?.textContent?.trim()).toBe('Renamed Corp');
    expect(el(fixture).querySelector('.header__brand')?.textContent?.trim()).toBe('Renamed Corp');
  });

  it('shows the notification bell linking to the center and hides the dot when there are none', () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();

    const bell = el(fixture).querySelector('[data-action="notifications"]') as HTMLAnchorElement;
    expect(bell).not.toBeNull();
    expect(bell.getAttribute('href')).toBe('/notifications');
    expect(bell.getAttribute('aria-label')).toBe('Notifications');
    expect(el(fixture).querySelector('[data-dot]')).toBeNull();
  });

  it('shows the unread dot when the count is greater than zero', () => {
    mockViewport(true);
    asUser('Manager');
    unreadCountSignal.set(3);
    const fixture = createComponent();

    expect(el(fixture).querySelector('[data-dot]')).not.toBeNull();
  });

  it('refreshes the unread count on the interval', fakeAsync(() => {
    mockViewport(true);
    asUser('Manager');
    createComponent();

    // Refresh on mount so the badge is accurate as soon as the shell appears.
    expect(refreshUnreadCountSpy).toHaveBeenCalled();

    refreshUnreadCountSpy.calls.reset();
    tick(SESSION_CHECK_INTERVAL_MS);
    expect(refreshUnreadCountSpy).toHaveBeenCalledTimes(1);
    expect(refreshMailCountSpy).toHaveBeenCalled();

    discardPeriodicTasks();
  }));

  it('resolves the header title for the notifications route', async () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();

    await TestBed.inject(Router).navigateByUrl('/notifications');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.header__title')?.textContent).toContain('Notifications');
  });

  it('shows Reminder Settings to Managers and resolves its header title', async () => {
    mockViewport(true);
    asUser('Manager');
    const fixture = createComponent();

    expandParent(fixture, 'Settings');
    const settings = Array.from(el(fixture).querySelectorAll('a.nav__item')).find(
      (link) => link.querySelector('.nav__text')?.textContent?.trim() === 'Reminder Settings'
    );
    expect(settings?.getAttribute('href')).toBe('/settings/reminders');

    await TestBed.inject(Router).navigateByUrl('/settings/reminders');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.header__title')?.textContent).toContain('Reminder Settings');
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

  for (const role of ['Administrator', 'Manager', 'Employee'] as const) {
    it(`shows the Mail entry with its unread badge to ${role}s`, () => {
      mockViewport(true);
      asUser(role);
      mailUnreadSignal.set(4);
      const fixture = createComponent();
      expandParent(fixture, 'Communication Management');

      const mail = Array.from(el(fixture).querySelectorAll('a.nav__item')).find(
        (link) => link.querySelector('.nav__text')?.textContent?.trim() === 'Mail'
      );
      expect(mail?.getAttribute('href')).toBe('/mail');
      expect(mail?.querySelector('[data-badge="mail"]')?.textContent?.trim()).toBe('4');
      expect(refreshMailCountSpy).toHaveBeenCalled();
    });
  }

  it('hides the Mail badge when nothing is unread', () => {
    mockViewport(true);
    asUser('Employee');
    const fixture = createComponent();
    expandParent(fixture, 'Communication Management');

    expect(el(fixture).querySelector('[data-badge="mail"]')).toBeNull();
  });
});
