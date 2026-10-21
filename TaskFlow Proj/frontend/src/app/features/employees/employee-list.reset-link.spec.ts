import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { PasswordResetRequest, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { PasswordResetService } from '../../core/services/password-reset.service';
import { ProjectService } from '../../core/services/project.service';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { fakeAuthService } from '../../testing/permissions';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';
import { EmployeeListComponent } from './employee-list.component';

describe('EmployeeListComponent password reset mail link', () => {
  const manager: User = {
    id: 2,
    employeeCode: 'MGR-001',
    fullName: 'Manager',
    username: 'manager',
    email: 'manager@taskflow.com',
    role: 'Manager',
    roleId: 2,
    isActive: true,
    createdAt: '2026-10-19T00:00:00Z',
    lastLoginAt: null
  };

  const reference = 'f0414391-a984-49bf-a59d-a1c90c4115a9';

  function request(overrides: Partial<PasswordResetRequest> = {}): PasswordResetRequest {
    return {
      referenceId: reference,
      userId: 2,
      fullName: 'Manager',
      username: 'manager',
      email: 'manager@taskflow.com',
      roleName: 'Manager',
      origin: 'LoginScreen',
      status: 'Pending',
      createdAt: '2026-10-21T11:30:00Z',
      completedAt: null,
      adminNotified: true,
      userNotified: false,
      temporaryPasswordIssued: false,
      failureReason: null,
      ...overrides
    };
  }

  let list: jasmine.Spy;
  let getById: jasmine.Spy;
  const currentUser = signal<{ id: number; permissions: string[] } | null>(null);

  function create(queryParams: Record<string, string>, permissions: string[]): ComponentFixture<EmployeeListComponent> {
    currentUser.set({ id: 1, permissions });
    TestBed.configureTestingModule({
      imports: [EmployeeListComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { getAll: () => of([manager]), getById } },
        { provide: ProjectService, useValue: { getAll: () => of([]) } },
        { provide: DepartmentService, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: fakeAuthService(currentUser, { clearSession: () => undefined }) },
        { provide: RoleService, useValue: { list: () => of([]) } },
        { provide: PasswordResetService, useValue: { list, process: () => of({ status: 'Completed' }), retryNotification: () => of({ status: 'Completed' }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeListComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<EmployeeListComponent>) => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    list = jasmine.createSpy('list').and.returnValue(of([request()]));
    getById = jasmine.createSpy('getById').and.returnValue(of(manager));
  });

  afterEach(() => document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close()));

  it('opens the requesting employee\'s details with the linked request highlighted', () => {
    const fixture = create({ resetRequest: reference }, [Permissions.EmployeesManage, Permissions.EmployeesView]);
    fixture.detectChanges();

    expect(getById).toHaveBeenCalledOnceWith(2);
    const panel = fixture.debugElement.nativeElement.querySelector('app-employee-details-panel') as HTMLElement;
    expect(panel).toBeTruthy();
    expect(fixture.componentInstance.focusRequest()).toBe(reference);
    expect(panel.querySelector(`[data-request="${reference}"][data-focused="true"]`)).toBeTruthy();
  });

  it('matches the reference regardless of letter case', () => {
    const fixture = create({ resetRequest: reference.toUpperCase() }, [Permissions.EmployeesManage]);
    fixture.detectChanges();

    expect(getById).toHaveBeenCalledOnceWith(2);
    expect(fixture.componentInstance.focusRequest()).toBe(reference);
  });

  it('says so and opens nothing for an unknown reference', () => {
    const fixture = create({ resetRequest: '00000000-0000-0000-0000-000000000000' }, [Permissions.EmployeesManage]);
    fixture.detectChanges();

    expect(getById).not.toHaveBeenCalled();
    expect(fixture.componentInstance.panel()).toBeNull();
    expect(el(fixture).querySelector('[data-error="reset-link"]')?.textContent).toContain('No password reset request matches');
  });

  it('shows the failure instead of failing silently when the request list cannot be read', () => {
    list.and.returnValue(throwError(() => ({ status: 500, error: { error: 'Server problem.' } })));
    const fixture = create({ resetRequest: reference }, [Permissions.EmployeesManage]);
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-error="reset-link"]')?.textContent).toContain('Server problem.');
  });

  it('does nothing for a caller without employees.manage', () => {
    const fixture = create({ resetRequest: reference }, [Permissions.EmployeesView]);
    fixture.detectChanges();

    expect(list).not.toHaveBeenCalled();
    expect(fixture.componentInstance.panel()).toBeNull();
  });

  it('does nothing without a reference in the address', () => {
    const fixture = create({}, [Permissions.EmployeesManage]);
    fixture.detectChanges();

    expect(list).not.toHaveBeenCalled();
    expect(fixture.componentInstance.focusRequest()).toBeNull();
  });
});

describe('EmployeeDetailsPanelComponent linked request', () => {
  const employee: User = {
    id: 2,
    employeeCode: 'MGR-001',
    fullName: 'Manager',
    username: 'manager',
    email: 'manager@taskflow.com',
    role: 'Manager',
    roleId: 2,
    isActive: true,
    createdAt: '2026-10-19T00:00:00Z',
    lastLoginAt: null
  };

  function req(referenceId: string, status: PasswordResetRequest['status']): PasswordResetRequest {
    return {
      referenceId,
      userId: 2,
      fullName: 'Manager',
      username: 'manager',
      email: 'manager@taskflow.com',
      roleName: 'Manager',
      origin: 'LoginScreen',
      status,
      createdAt: '2026-10-21T11:30:00Z',
      completedAt: null,
      adminNotified: true,
      userNotified: false,
      temporaryPasswordIssued: false,
      failureReason: null
    };
  }

  function render(requests: PasswordResetRequest[], focus: string | null): ComponentFixture<EmployeeDetailsPanelComponent> {
    TestBed.configureTestingModule({
      imports: [EmployeeDetailsPanelComponent],
      providers: [
        { provide: PasswordResetService, useValue: { list: () => of(requests) } },
        { provide: AuthService, useValue: { hasPermission: () => true } }
      ]
    });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.componentRef.setInput('focusRequest', focus);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => document.querySelectorAll('app-employee-details-panel').forEach((node) => node.remove()));

  it('highlights the linked pending request and focuses its password field', async () => {
    const fixture = render([req('b', 'Pending')], 'b');
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-request="b"]')?.getAttribute('data-focused')).toBe('true');
    expect(document.activeElement).toBe(element.querySelector('[data-request="b"] [data-field="supplied-password"]'));
    expect(element.querySelector('[data-field="focus-note"]')).toBeNull();
  });

  it('explains when the linked request is already handled', () => {
    const fixture = render([req('a', 'Completed')], 'a');

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-field="focus-note"]')?.textContent).toContain(
      'already completed'
    );
  });

  it('explains when the linked request is not this employee\'s', () => {
    const fixture = render([req('a', 'Pending')], 'someone-elses');

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-field="focus-note"]')?.textContent).toContain(
      'does not belong'
    );
  });

  it('shows no note and focuses nothing without a link', () => {
    const fixture = render([req('a', 'Pending')], null);

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-field="focus-note"]')).toBeNull();
    expect(element.querySelector('[data-focused="true"]')).toBeNull();
  });
});
