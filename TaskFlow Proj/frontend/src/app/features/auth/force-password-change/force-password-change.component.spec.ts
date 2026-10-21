import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../../core/auth/login-reasons';
import { ProfileService } from '../../../core/services/profile.service';
import { ForcePasswordChangeComponent } from './force-password-change.component';

describe('ForcePasswordChangeComponent', () => {
  let fixture: ComponentFixture<ForcePasswordChangeComponent>;
  let component: ForcePasswordChangeComponent;
  let changePassword: jasmine.Spy;
  let authService: jasmine.SpyObj<AuthService>;
  let navigate: jasmine.Spy;

  const el = () => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    changePassword = jasmine.createSpy('changePassword').and.returnValue(of(undefined));
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession']);
    TestBed.configureTestingModule({
      imports: [ForcePasswordChangeComponent],
      providers: [
        { provide: ProfileService, useValue: { changePassword } },
        { provide: AuthService, useValue: authService },
        provideRouter([])
      ]
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ForcePasswordChangeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows exactly New Password, Confirm Password and Update Password', () => {
    const inputs = Array.from(el().querySelectorAll('input')).map((input) => input.getAttribute('data-field'));
    expect(inputs).toEqual(['newPassword', 'confirmPassword']);
    expect(el().querySelector('[data-field="currentPassword"]')).toBeNull();
    expect(el().querySelector('[data-action="change-password"]')?.textContent).toContain('Update Password');
  });

  it('offers no way out: no cancel, close, skip or navigation links', () => {
    expect(el().querySelector('a, [data-action="cancel"], [data-action="close"], [data-action="skip"]')).toBeNull();
    const buttons = Array.from(el().querySelectorAll('button'));
    expect(buttons.length).toBe(1);
    expect(buttons[0].getAttribute('type')).toBe('submit');
  });

  it('shows no errors on the untouched screen', () => {
    expect(el().querySelectorAll('[data-error]').length).toBe(0);
  });

  it('shows "Passwords do not match." under Confirm Password as soon as it is typed wrong', () => {
    const newPassword = el().querySelector('[data-field="newPassword"]') as HTMLInputElement;
    const confirm = el().querySelector('[data-field="confirmPassword"]') as HTMLInputElement;

    newPassword.value = 'Fresh-P@ss1';
    newPassword.dispatchEvent(new Event('input'));
    confirm.value = 'Fresh-P@ss2';
    confirm.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const errors = Array.from(el().querySelectorAll('[data-error]'));
    expect(errors.length).toBe(1);
    expect(errors[0].getAttribute('data-error')).toBe('confirmPassword');
    expect(errors[0].textContent).toContain('Passwords do not match.');
    expect(errors[0].getAttribute('role')).toBe('alert');

    // Correcting it clears the message.
    confirm.value = 'Fresh-P@ss1';
    confirm.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el().querySelectorAll('[data-error]').length).toBe(0);
  });

  it('blocks a mismatched or too-short password with field errors', () => {
    component.form.setValue({ newPassword: 'abc', confirmPassword: 'abd' });

    component.submit();
    fixture.detectChanges();

    expect(changePassword).not.toHaveBeenCalled();
    expect(component.fieldError('newPassword')).toContain('at least 6');
    expect(component.fieldError('confirmPassword')).toBe('Passwords do not match.');
  });

  it('sends only the new password, ends the local session and sends the user to sign in again', () => {
    component.form.setValue({ newPassword: 'Fresh-P@ss1', confirmPassword: 'Fresh-P@ss1' });

    component.submit();

    expect(changePassword).toHaveBeenCalledOnceWith({ newPassword: 'Fresh-P@ss1' });
    expect(authService.clearSession).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
  });

  it('shows the server message and stays when the change is refused', () => {
    changePassword.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Password must be at least 6 characters.' } }))
    );
    component.form.setValue({ newPassword: 'Fresh-P@ss1', confirmPassword: 'Fresh-P@ss1' });

    component.submit();
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('at least 6');
    expect(authService.clearSession).not.toHaveBeenCalled();
  });
});
