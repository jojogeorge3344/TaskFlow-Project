import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Permissions } from '../../core/auth/permissions';
import { AuthService } from '../../core/auth/auth.service';
import { PasswordResetRequest, User } from '../../core/models';
import { PasswordResetService } from '../../core/services/password-reset.service';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';

describe('EmployeeDetailsPanelComponent password reset requests', () => {
  const employee: User = {
    id: 7,
    employeeCode: 'EMP-022',
    fullName: 'Rahul Menon',
    username: 'rahul.m',
    email: 'rahul.menon@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-09-05T00:00:00Z',
    lastLoginAt: null
  };

  function request(overrides: Partial<PasswordResetRequest> = {}): PasswordResetRequest {
    return {
      referenceId: 'ref-1',
      userId: 7,
      fullName: 'Rahul Menon',
      username: 'rahul.m',
      email: 'rahul.menon@taskflow.com',
      roleName: 'Employee',
      origin: 'LoginScreen',
      status: 'Pending',
      createdAt: '2026-10-21T08:00:00Z',
      completedAt: null,
      adminNotified: true,
      userNotified: false,
      temporaryPasswordIssued: false,
      failureReason: null,
      ...overrides
    };
  }

  let list: jasmine.Spy;
  let process: jasmine.Spy;
  let retryNotification: jasmine.Spy;

  function render(permissions: string[]): ComponentFixture<EmployeeDetailsPanelComponent> {
    TestBed.configureTestingModule({
      imports: [EmployeeDetailsPanelComponent],
      providers: [
        { provide: PasswordResetService, useValue: { list, process, retryNotification } },
        { provide: AuthService, useValue: { hasPermission: (key: string) => permissions.includes(key) } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<EmployeeDetailsPanelComponent>) => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    list = jasmine.createSpy('list').and.returnValue(of([request(), request({ referenceId: 'other', userId: 99 })]));
    process = jasmine.createSpy('process').and.returnValue(of({ status: 'Completed' }));
    retryNotification = jasmine.createSpy('retryNotification').and.returnValue(of({ status: 'Completed' }));
  });

  it('lists this employee\'s requests with origin, status and created time', () => {
    const fixture = render([Permissions.EmployeesManage]);

    const items = el(fixture).querySelectorAll('[data-request]');
    expect(items.length).toBe(1);
    expect(items[0].querySelector('[data-field="request-status"]')?.textContent).toContain('Pending');
    expect(items[0].querySelector('[data-field="request-origin"]')?.textContent).toContain('Login Screen');
    expect(items[0].querySelector('[data-field="request-created"]')?.textContent?.trim()).not.toBe('');
  });

  it('shows only the latest request when an employee has raised several, with no history', () => {
    list.and.returnValue(
      of([
        request({ referenceId: 'old', createdAt: '2026-10-21T08:00:00Z' }),
        request({ referenceId: 'newest', createdAt: '2026-10-21T11:30:00Z' }),
        request({ referenceId: 'middle', createdAt: '2026-10-21T10:00:00Z', status: 'Completed' })
      ])
    );
    const fixture = render([Permissions.EmployeesManage]);

    const items = el(fixture).querySelectorAll('[data-request]');
    expect(items.length).toBe(1);
    expect(items[0].getAttribute('data-request')).toBe('newest');
    expect(el(fixture).textContent).not.toContain('history');
  });

  it('says a newer request exists when the link names an older one', () => {
    list.and.returnValue(
      of([
        request({ referenceId: 'old', createdAt: '2026-10-21T08:00:00Z' }),
        request({ referenceId: 'newest', createdAt: '2026-10-21T11:30:00Z' })
      ])
    );
    TestBed.configureTestingModule({
      imports: [EmployeeDetailsPanelComponent],
      providers: [
        { provide: PasswordResetService, useValue: { list, process, retryNotification } },
        { provide: AuthService, useValue: { hasPermission: () => true } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.componentRef.setInput('focusRequest', 'old');
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-field="focus-note"]')?.textContent).toContain('A newer password reset request exists');
    expect(el(fixture).querySelectorAll('[data-request]').length).toBe(1);
  });

  it('blocks processing until the administrator types a password', () => {
    const fixture = render([Permissions.EmployeesManage]);

    (el(fixture).querySelector('[data-action="process-reset"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(process).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="password-reset"]')?.textContent).toContain('Enter the new password');
    expect(el(fixture).querySelector('[data-action="generate"]')).toBeNull();
  });

  it('sets the typed password for a Login Screen request and refreshes the list', () => {
    const fixture = render([Permissions.EmployeesManage]);
    expect(el(fixture).querySelector('[data-field="supplied-password"]')).toBeTruthy();

    fixture.componentInstance.setSupplied('ref-1', 'Issued-P@ss1');
    (el(fixture).querySelector('[data-action="process-reset"]') as HTMLButtonElement).click();

    expect(process).toHaveBeenCalledOnceWith('ref-1', 'Issued-P@ss1');
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('never displays the typed password afterwards', () => {
    list.and.returnValue(of([request({ origin: 'ProfileScreen' })]));
    const fixture = render([Permissions.EmployeesManage]);

    fixture.componentInstance.setSupplied('ref-1', '  Chosen-P@ss1 ');
    (el(fixture).querySelector('[data-action="process-reset"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(process).toHaveBeenCalledOnceWith('ref-1', 'Chosen-P@ss1');
    expect(fixture.componentInstance.supplied()['ref-1']).toBeUndefined();
    expect(el(fixture).textContent).not.toContain('Chosen-P@ss1');
  });

  it('retries the notification of a failed request', () => {
    list.and.returnValue(of([request({ status: 'Failed', failureReason: 'Mail could not be written.' })]));
    const fixture = render([Permissions.EmployeesManage]);

    expect(el(fixture).querySelector('[data-action="process-reset"]')).toBeNull();
    expect(el(fixture).querySelector('[data-field="request-failure"]')?.textContent).toContain('Mail could not');

    (el(fixture).querySelector('[data-action="retry-notification"]') as HTMLButtonElement).click();

    expect(retryNotification).toHaveBeenCalledOnceWith('ref-1');
  });

  it('never renders the section or calls the API without employees.manage', () => {
    const fixture = render([Permissions.EmployeesView]);

    expect(el(fixture).querySelector('[data-section="password-reset-requests"]')).toBeNull();
    expect(list).not.toHaveBeenCalled();
  });

  it('surfaces a 403 as an error message instead of failing silently', () => {
    list.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 403, error: { title: 'Forbidden', detail: 'You may not do that.' } }))
    );
    const fixture = render([Permissions.EmployeesManage]);
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-error="password-reset"]')?.textContent).toContain('You may not do that.');
  });
});

describe('EmployeeDetailsPanelComponent login link', () => {
  const employee: User = {
    id: 7,
    employeeCode: 'EMP-022',
    fullName: 'Rahul Menon',
    username: 'rahul.m',
    email: 'rahul.menon@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-09-05T00:00:00Z',
    lastLoginAt: null
  };

  function request(status: PasswordResetRequest['status']): PasswordResetRequest {
    return {
      referenceId: 'ref-1',
      userId: 7,
      fullName: 'Rahul Menon',
      username: 'rahul.m',
      email: 'rahul.menon@taskflow.com',
      roleName: 'Employee',
      origin: 'LoginScreen',
      status,
      createdAt: '2026-10-21T08:00:00Z',
      completedAt: null,
      adminNotified: true,
      userNotified: false,
      temporaryPasswordIssued: true,
      failureReason: null
    };
  }

  let loginLink: jasmine.Spy;

  function render(status: PasswordResetRequest['status']): ComponentFixture<EmployeeDetailsPanelComponent> {
    loginLink = jasmine.createSpy('loginLink').and.returnValue(of({ path: '/login/enc-token_123', url: null }));
    TestBed.configureTestingModule({
      imports: [EmployeeDetailsPanelComponent],
      providers: [
        { provide: PasswordResetService, useValue: { list: () => of([request(status)]), loginLink } },
        { provide: AuthService, useValue: { hasPermission: () => true } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<EmployeeDetailsPanelComponent>) => fixture.nativeElement as HTMLElement;

  it('offers the login link only for a completed request', () => {
    expect(el(render('Completed')).querySelector('[data-action="show-login-link"]')).toBeTruthy();
    TestBed.resetTestingModule();
    expect(el(render('Pending')).querySelector('[data-action="show-login-link"]')).toBeNull();
    TestBed.resetTestingModule();
    expect(el(render('Failed')).querySelector('[data-action="show-login-link"]')).toBeNull();
  });

  it('shows the encrypted link on this application\'s address, with no password in it', () => {
    const fixture = render('Completed');

    (el(fixture).querySelector('[data-action="show-login-link"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(loginLink).toHaveBeenCalledOnceWith('ref-1');
    const field = el(fixture).querySelector('[data-field="login-link"]') as HTMLInputElement;
    expect(field.value).toBe(`${window.location.origin}/login/enc-token_123`);
    expect(field.readOnly).toBeTrue();
    expect(field.value).not.toContain('@');
  });

  it('copies the link', async () => {
    const fixture = render('Completed');
    (el(fixture).querySelector('[data-action="show-login-link"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const writeText = jasmine.createSpy('writeText').and.resolveTo();
    spyOnProperty(navigator, 'clipboard', 'get').and.returnValue({ writeText } as unknown as Clipboard);

    (el(fixture).querySelector('[data-action="copy-login-link"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledOnceWith(`${window.location.origin}/login/enc-token_123`);
    expect(el(fixture).querySelector('[data-status="link-copied"]')).toBeTruthy();
  });

  it('shows an error when the link cannot be fetched', () => {
    const fixture = render('Completed');
    loginLink.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'Not the latest.' } })));

    (el(fixture).querySelector('[data-action="show-login-link"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-error="password-reset"]')?.textContent).toContain('Not the latest.');
    expect(el(fixture).querySelector('[data-field="login-link"]')).toBeNull();
  });
});

describe('EmployeeDetailsPanelComponent login link note', () => {
  it('says the link lasts one minute and works once, and carries no password', () => {
    TestBed.configureTestingModule({
      imports: [EmployeeDetailsPanelComponent],
      providers: [
        {
          provide: PasswordResetService,
          useValue: {
            list: () =>
              of([
                {
                  referenceId: 'ref-1',
                  userId: 7,
                  fullName: 'Rahul Menon',
                  username: 'rahul.m',
                  email: 'rahul.menon@taskflow.com',
                  roleName: 'Employee',
                  origin: 'LoginScreen',
                  status: 'Completed',
                  createdAt: '2026-10-21T08:00:00Z',
                  completedAt: null,
                  adminNotified: true,
                  userNotified: true,
                  temporaryPasswordIssued: true,
                  failureReason: null
                }
              ]),
            loginLink: () => of({ path: '/login/enc', url: null })
          }
        },
        { provide: AuthService, useValue: { hasPermission: () => true } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', {
      id: 7,
      employeeCode: 'EMP-022',
      fullName: 'Rahul Menon',
      username: 'rahul.m',
      email: 'rahul.menon@taskflow.com',
      role: 'Employee',
      roleId: 3,
      isActive: true,
      createdAt: '2026-09-05T00:00:00Z',
      lastLoginAt: null
    } as User);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-action="show-login-link"]')?.textContent).toContain('Generate login link');
    (element.querySelector('[data-action="show-login-link"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const note = element.querySelector('[data-field="link-note"]')?.textContent ?? '';
    expect(note).toContain('works once');
    expect(note).toContain('1 minute');
    expect(note).toContain('does not contain the password');
  });
});
