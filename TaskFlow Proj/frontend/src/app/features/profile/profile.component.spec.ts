import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ProfileComponent } from './profile.component';

describe('ProfileComponent', () => {
  const me: User = {
    id: 3,
    employeeCode: 'EMP-3',
    fullName: 'Jo Jo',
    username: 'jojo',
    email: 'jojo@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    departmentId: 1,
    departmentName: 'Engineering'
  };

  let getSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let changePasswordSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(me));
    updateSpy = jasmine.createSpy('update');
    changePasswordSpy = jasmine.createSpy('changePassword');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession', 'applyProfile']);
  });

  function create(): ComponentFixture<ProfileComponent> {
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: getSpy, update: updateSpy, changePassword: changePasswordSpy } },
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toast },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    router = TestBed.inject(Router);
    return fixture;
  }

  function el(fixture: ComponentFixture<ProfileComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(fixture: ComponentFixture<ProfileComponent>, field: string): HTMLInputElement {
    return el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;
  }

  function submit(fixture: ComponentFixture<ProfileComponent>, action: string): void {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('shows the own profile with read-only account fields', () => {
    const fixture = create();

    expect(getSpy).toHaveBeenCalled();
    expect(input(fixture, 'fullName').value).toBe('Jo Jo');
    expect(input(fixture, 'email').value).toBe('jojo');
    expect(el(fixture).querySelector('[data-email-suffix]')?.textContent?.trim()).toBe('@taskflow.com');
    expect(el(fixture).querySelector('[data-field="readonly-Username"]')?.textContent?.trim()).toBe('jojo');
    expect(el(fixture).querySelector('[data-field="readonly-Role"]')?.textContent?.trim()).toBe('Employee');
    expect(el(fixture).querySelector('[data-field="readonly-Project"]')?.textContent?.trim()).toBe('Engineering');
    expect(el(fixture).querySelector('[data-field="readonly-Account status"]')?.textContent?.trim()).toBe('Active');
  });

  it('shows the employee their own manager', () => {
    getSpy.and.returnValue(of({ ...me, managerId: 20, managerName: 'Anupama Nair' }));
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="readonly-Manager"]')?.textContent?.trim()).toBe('Anupama Nair');
    expect(el(fixture).querySelector('[data-field="readonly-Team"]')).toBeNull();
  });

  it('shows a manager their departments and team', () => {
    getSpy.and.returnValue(
      of({
        ...me,
        role: 'Manager',
        roleId: 2,
        managerDepartments: [{ id: 1, name: 'Engineering' }],
        reports: [{ id: 9, fullName: 'Sara' }]
      })
    );
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="readonly-Manager"]')?.textContent?.trim()).toBe('—');
    expect(el(fixture).querySelector('[data-field="readonly-Managed projects"]')?.textContent?.trim()).toBe('Engineering');
    expect(el(fixture).querySelector('[data-field="readonly-Team"]')?.textContent?.trim()).toBe('Sara');
  });

  it('saves the self-edit and refreshes the cached session user', () => {
    const updated: User = { ...me, fullName: 'Joanna Jo', email: 'joanna@taskflow.com' };
    updateSpy.and.returnValue(of(updated));
    const fixture = create();

    fixture.componentInstance.form.patchValue({ fullName: 'Joanna Jo', email: 'joanna' });
    submit(fixture, 'save');

    expect(updateSpy).toHaveBeenCalledOnceWith({ fullName: 'Joanna Jo', email: 'joanna@taskflow.com' });
    expect(authService.applyProfile).toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith('Profile updated');
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();
  });

  it('shows a rejected duplicate email from the server', () => {
    updateSpy.and.returnValue(
      throwError(
        () => new HttpErrorResponse({ status: 400, error: { error: 'Email already exists.' } })
      )
    );
    const fixture = create();

    submit(fixture, 'save');

    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Email already exists.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('blocks a malformed email client-side', () => {
    const fixture = create();

    fixture.componentInstance.form.controls.email.setValue('bad name!');
    fixture.detectChanges();
    submit(fixture, 'save');

    expect(updateSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="email"]')?.textContent).toContain('before @taskflow.com');
  });

  it('keeps only the part before the @ when a full address is typed or pasted', () => {
    const updated: User = { ...me, email: 'joanna@taskflow.com' };
    updateSpy.and.returnValue(of(updated));
    const fixture = create();
    const field = input(fixture, 'email');

    field.value = 'Joanna@other.com';
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(field.value).toBe('joanna');
    expect(el(fixture).querySelector('[data-email-suffix]')?.textContent?.trim()).toBe('@taskflow.com');

    submit(fixture, 'save');
    expect(updateSpy).toHaveBeenCalledOnceWith(jasmine.objectContaining({ email: 'joanna@taskflow.com' }));
  });

  it('requires matching password confirmation', () => {
    const fixture = create();

    fixture.componentInstance.passwordForm.setValue({
      currentPassword: 'P@ssw0rd!',
      newPassword: 'N3wP@ss!',
      confirmPassword: 'different'
    });
    fixture.detectChanges();
    submit(fixture, 'change-password');

    expect(changePasswordSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="confirmPassword"]')?.textContent).toContain('do not match');
  });

  it('changes the password and signs the user out', () => {
    changePasswordSpy.and.returnValue(of(undefined));
    const fixture = create();
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture.componentInstance.passwordForm.setValue({
      currentPassword: 'P@ssw0rd!',
      newPassword: 'N3wP@ss!',
      confirmPassword: 'N3wP@ss!'
    });
    submit(fixture, 'change-password');

    expect(changePasswordSpy).toHaveBeenCalledOnceWith({ currentPassword: 'P@ssw0rd!', newPassword: 'N3wP@ss!' });
    expect(authService.clearSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED }
    });
  });

  it('shows a wrong current password error without signing out', () => {
    changePasswordSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Current password is incorrect.' } }))
    );
    const fixture = create();

    fixture.componentInstance.passwordForm.setValue({
      currentPassword: 'wrong',
      newPassword: 'N3wP@ss!',
      confirmPassword: 'N3wP@ss!'
    });
    submit(fixture, 'change-password');

    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Current password is incorrect.');
    expect(authService.clearSession).not.toHaveBeenCalled();
  });

  it('shows a retryable error when loading fails', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load your profile.');

    getSpy.and.returnValue(of(me));
    (el(fixture).querySelector('[data-action="retry"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(input(fixture, 'fullName').value).toBe('Jo Jo');
  });
});
