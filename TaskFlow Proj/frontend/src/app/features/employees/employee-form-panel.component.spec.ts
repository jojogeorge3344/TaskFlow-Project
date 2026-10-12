import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';
import { EmployeeFormPanelComponent } from './employee-form-panel.component';

const existing: User = {
  id: 7,
  employeeCode: 'EMP-022',
  fullName: 'Rahul Menon',
  username: 'rahul.m',
  email: 'rahul.menon@taskflow.local',
  role: 'Employee',
  isActive: true,
  createdAt: '2026-09-05T00:00:00Z',
  lastLoginAt: null,
  departmentId: 1,
  departmentName: 'Engineering'
};

describe('EmployeeFormPanelComponent', () => {
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let toastSpy: jasmine.Spy;
  let clearSessionSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;
  const currentUser = signal<AuthUser | null>(null);

  function create(employee: User | null): ComponentFixture<EmployeeFormPanelComponent> {
    const fixture = TestBed.createComponent(EmployeeFormPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.componentRef.setInput('departments', [
      { id: 1, name: 'Engineering', description: null, createdAt: '2026-09-01', employeeCount: 1 }
    ]);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<EmployeeFormPanelComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function type(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: string, value: string): void {
    const input = el(fixture).querySelector(`#${id}`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submit(fixture: ComponentFixture<EmployeeFormPanelComponent>): void {
    (el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    createSpy = jasmine.createSpy('create');
    updateSpy = jasmine.createSpy('update');

    currentUser.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.local', role: 'Administrator', employeeCode: null });
    clearSessionSpy = jasmine.createSpy('clearSession');

    await TestBed.configureTestingModule({
      imports: [EmployeeFormPanelComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { create: createSpy, update: updateSpy } },
        { provide: AuthService, useValue: { currentUser: currentUser.asReadonly(), clearSession: clearSessionSpy } }
      ]
    }).compileComponents();

    toastSpy = spyOn(TestBed.inject(ToastService), 'success');
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('keeps the current password when the new password is left blank', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of({ ...existing, fullName: 'Rahul M' }));

    type(fixture, 'emp-name', 'Rahul M');
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: null }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul M updated');
  });

  it('sends a new password and warns that the employee must sign in again', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of(existing));

    type(fixture, 'emp-password', 'NewPass#1');
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: 'NewPass#1' }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul Menon updated. They must sign in again.');
    expect(clearSessionSpy).not.toHaveBeenCalled();
  });

  it('rejects a new password shorter than 6 characters when editing', () => {
    const fixture = create(existing);

    type(fixture, 'emp-password', '123');
    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Use at least 6 characters.');
    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('rejects a new password that is only spaces', () => {
    const fixture = create(existing);

    type(fixture, 'emp-password', '      ');
    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Password cannot be only spaces.');
    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('signs the admin out at once when they change their own role', () => {
    const self: User = { ...existing, id: 1, username: 'admin', fullName: 'Admin', role: 'Administrator' };
    const fixture = create(self);
    updateSpy.and.returnValue(of({ ...self, role: 'Manager' }));

    const role = el(fixture).querySelector('#emp-role') as HTMLSelectElement;
    role.value = 'Manager';
    role.dispatchEvent(new Event('change'));
    submit(fixture);

    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'credentials-changed' } });
  });

  it('signs the admin out at once when they change their own username', () => {
    const self: User = { ...existing, id: 1, username: 'admin', fullName: 'Admin' };
    const fixture = create(self);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    updateSpy.and.returnValue(of({ ...self, username: 'admin2' }));

    type(fixture, 'emp-username', 'admin2');
    submit(fixture);

    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'credentials-changed' } });
    expect(savedSpy).not.toHaveBeenCalled();
  });

  it('shows employee code, password and status fields when adding', () => {
    const fixture = create(null);

    expect(el(fixture).querySelector('#emp-code')).toBeTruthy();
    expect(el(fixture).querySelector('#emp-password')).toBeTruthy();
    expect(el(fixture).querySelector('#emp-status')).toBeTruthy();
  });

  it('pre-fills the employee, offers an optional new password, and hides status when editing', () => {
    const fixture = create(existing);

    expect((el(fixture).querySelector('#emp-name') as HTMLInputElement).value).toBe('Rahul Menon');
    expect((el(fixture).querySelector('#emp-code') as HTMLInputElement).value).toBe('EMP-022');
    expect(el(fixture).querySelector('label[for="emp-password"]')?.textContent).toContain('New password');
    expect((el(fixture).querySelector('#emp-password') as HTMLInputElement).value).toBe('');
    expect(el(fixture).querySelector('#emp-password-hint')?.textContent).toContain('signs this employee out');
    expect(el(fixture).querySelector('#emp-status')).toBeNull();
  });

  it('shows inline required and email errors on submit without saving', () => {
    const fixture = create(null);
    type(fixture, 'emp-email', 'not-an-email');

    submit(fixture);

    const errors = Array.from(el(fixture).querySelectorAll('.tf-field__error')).map((node) => node.textContent?.trim());
    expect(errors).toContain('Full name is required.');
    expect(errors).toContain('Username is required.');
    expect(errors).toContain('Enter a valid email address.');
    expect(errors).toContain('Password is required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('requires a password of at least 6 characters, like the API', () => {
    const fixture = create(null);
    type(fixture, 'emp-password', '12345');

    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Use at least 6 characters.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('creates the employee, shows a success toast and emits saved', () => {
    const fixture = create(null);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    createSpy.and.returnValue(of({ ...existing, id: 9, fullName: 'Divya Krishnan' }));

    type(fixture, 'emp-name', '  Divya Krishnan ');
    type(fixture, 'emp-username', 'divya.k');
    type(fixture, 'emp-email', 'divya@taskflow.local');
    type(fixture, 'emp-code', 'EMP-047');
    type(fixture, 'emp-password', 'Temp#1234');
    submit(fixture);

    expect(createSpy).toHaveBeenCalledWith(
      jasmine.objectContaining({
        fullName: 'Divya Krishnan',
        employeeCode: 'EMP-047',
        password: 'Temp#1234',
        isActive: true,
        role: 'Employee'
      })
    );
    expect(toastSpy).toHaveBeenCalledWith('Divya Krishnan added');
    expect(savedSpy).toHaveBeenCalled();
  });

  it('keeps the panel open and shows the server error on a 409 conflict', () => {
    const fixture = create(existing);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    updateSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Email is already in use.' } }))
    );

    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ email: 'rahul.menon@taskflow.local' }));
    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Email is already in use.');
    expect(savedSpy).not.toHaveBeenCalled();
    expect(toastSpy).not.toHaveBeenCalled();
  });

  it('disables Save while saving', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(new Subject<User>());

    submit(fixture);

    expect((el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
  });
});

describe('EmployeeDetailsPanelComponent', () => {
  it('shows read-only details and emits edit', async () => {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', existing);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const editSpy = jasmine.createSpy('edit');
    fixture.componentInstance.edit.subscribe(editSpy);

    expect(element.textContent).toContain('EMP-022');
    expect(element.textContent).toContain('rahul.m');
    expect(element.textContent).toContain('Engineering');
    expect(element.textContent).toContain('Never');
    expect(element.querySelector('input')).toBeNull();

    (element.querySelector('.tf-btn--primary') as HTMLButtonElement).click();
    expect(editSpy).toHaveBeenCalled();
  });
});
