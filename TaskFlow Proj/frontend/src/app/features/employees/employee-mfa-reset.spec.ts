import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, flush, flushMicrotasks } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { ProjectService } from '../../core/services/project.service';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { fakeAuthService } from '../../testing/permissions';
import { ToastService } from '../../shared/ui/toast.service';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';
import { EmployeeListComponent } from './employee-list.component';
import { NO_ALLOCATIONS, withProject } from '../../testing/users';

const withMfa: User = {
  id: 1,
  employeeCode: 'EMP-001',
  fullName: 'Rahul Menon',
  username: 'rahul.m',
  email: 'rahul.menon@taskflow.com',
  role: 'Employee',
  roleId: 3,
  isActive: true,
  createdAt: '2026-09-05T00:00:00Z',
  lastLoginAt: null,
  mfaEnabled: true,
  backupCodesRemaining: 7,
  ...NO_ALLOCATIONS
};

describe('EmployeeDetailsPanelComponent two-step status', () => {
  function create(employee: User, canResetMfa: boolean): ComponentFixture<EmployeeDetailsPanelComponent> {
    TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] });
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.componentRef.setInput('canResetMfa', canResetMfa);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<EmployeeDetailsPanelComponent>): HTMLElement => f.nativeElement as HTMLElement;

  it('shows whether two-step is on', () => {
    expect(el(create(withMfa, true)).querySelector('[data-field="mfa"]')?.textContent).toContain('On');
    TestBed.resetTestingModule();
    expect(el(create({ ...withMfa, mfaEnabled: false }, true)).querySelector('[data-field="mfa"]')?.textContent).toContain('Off');
  });

  it('offers the reset only to someone who may use it and only when MFA is on', () => {
    expect(el(create(withMfa, true)).querySelector('[data-action="reset-mfa"]')).toBeTruthy();
    TestBed.resetTestingModule();
    expect(el(create(withMfa, false)).querySelector('[data-action="reset-mfa"]')).toBeNull();
    TestBed.resetTestingModule();
    expect(el(create({ ...withMfa, mfaEnabled: false }, true)).querySelector('[data-action="reset-mfa"]')).toBeNull();
  });

  it('emits when the reset button is pressed', () => {
    const fixture = create(withMfa, true);
    let emitted = 0;
    fixture.componentInstance.resetMfa.subscribe(() => emitted++);

    (el(fixture).querySelector('[data-action="reset-mfa"]') as HTMLButtonElement).click();

    expect(emitted).toBe(1);
  });
});

describe('EmployeeListComponent MFA reset', () => {
  let resetSpy: jasmine.Spy;
  const currentUser = signal<{ id: number; permissions: string[] } | null>(null);

  function create(permissions: string[]): ComponentFixture<EmployeeListComponent> {
    currentUser.set({ id: 99, permissions });
    TestBed.configureTestingModule({
      imports: [EmployeeListComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { getAll: () => of([withMfa]), updateStatus: jasmine.createSpy(), delete: jasmine.createSpy(), resetMfa: resetSpy } },
        { provide: ProjectService, useValue: { getAll: () => of([]) } },
        { provide: DepartmentService, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: fakeAuthService(currentUser, { clearSession: () => undefined }) },
        { provide: RoleService, useValue: { assignable: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(EmployeeListComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<EmployeeListComponent>): HTMLElement => f.nativeElement as HTMLElement;

  function openDetails(fixture: ComponentFixture<EmployeeListComponent>): void {
    (el(fixture).querySelector('.person__name') as HTMLButtonElement).click();
    fixture.detectChanges();
    flush();
  }

  beforeEach(() => {
    resetSpy = jasmine.createSpy('resetMfa').and.returnValue(of(undefined));
  });

  afterEach(() => document.querySelectorAll('dialog[open]').forEach((d) => (d as HTMLDialogElement).close()));

  it('does nothing when the confirmation is cancelled', fakeAsync(() => {
    const fixture = create([Permissions.EmployeesView, Permissions.EmployeesMfaReset]);
    openDetails(fixture);

    (el(fixture).querySelector('[data-action="reset-mfa"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el(fixture).querySelector('dialog [data-action="cancel"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(resetSpy).not.toHaveBeenCalled();
  }));

  it('resets after confirmation, shows a toast and shows MFA as off', fakeAsync(() => {
    const fixture = create([Permissions.EmployeesView, Permissions.EmployeesMfaReset]);
    const toast = spyOn(TestBed.inject(ToastService), 'success');
    openDetails(fixture);

    (el(fixture).querySelector('[data-action="reset-mfa"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture).querySelector('dialog')?.textContent).toContain('Rahul Menon');
    (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(resetSpy).toHaveBeenCalledWith(1);
    expect(toast).toHaveBeenCalledWith('MFA reset for Rahul Menon');
    expect(el(fixture).querySelector('[data-field="mfa"]')?.textContent).toContain('Off');
    expect(el(fixture).querySelector('[data-action="reset-mfa"]')).toBeNull();
  }));

  it('shows the server message when the reset fails', fakeAsync(() => {
    resetSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404, error: { error: 'Not found.' } })));
    const fixture = create([Permissions.EmployeesView, Permissions.EmployeesMfaReset]);
    const toast = spyOn(TestBed.inject(ToastService), 'error');
    openDetails(fixture);

    (el(fixture).querySelector('[data-action="reset-mfa"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(toast).toHaveBeenCalledWith('Not found.');
  }));

  it('hides the reset from someone without employee management', fakeAsync(() => {
    const fixture = create([Permissions.EmployeesView]);
    openDetails(fixture);

    expect(el(fixture).querySelector('[data-action="reset-mfa"]')).toBeNull();
  }));
});
