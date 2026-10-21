import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../../core/auth/login-reasons';
import { PasswordResetService } from '../../../core/services/password-reset.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let component: ResetPasswordComponent;
  let completeFromLogin: jasmine.Spy;
  let navigate: jasmine.Spy;

  const el = () => fixture.nativeElement as HTMLElement;

  function create(state: Record<string, string> | null = { token: 'enc-token', email: 'manager@taskflow.com' }): void {
    completeFromLogin = jasmine.createSpy('completeFromLogin').and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [provideRouter([]), { provide: PasswordResetService, useValue: { completeFromLogin } }]
    });
    const router = TestBed.inject(Router);
    navigate = spyOn(router, 'navigate').and.resolveTo(true);
    spyOn(router, 'getCurrentNavigation').and.returnValue(state === null ? null : ({ extras: { state } } as never));
    history.replaceState(null, '');
    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function type(field: string, value: string): void {
    const input = el().querySelector(`[data-field="${field}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('shows the account and only New password and Confirm password - never a temporary password', () => {
    create();

    expect(el().querySelector('[data-field="account"]')?.textContent?.trim()).toBe('manager@taskflow.com');
    const inputs = Array.from(el().querySelectorAll('input')) as HTMLInputElement[];
    expect(inputs.map((input) => input.getAttribute('data-field'))).toEqual(['newPassword', 'confirmPassword']);
    expect(inputs.every((input) => input.value === '')).toBeTrue();
    expect(el().querySelector('[data-field="temporaryPassword"]')).toBeNull();
    expect(el().querySelector('[data-action="update-password"]')?.textContent).toContain('Update password');
  });

  it('offers no cancel, close, skip or link out', () => {
    create();

    expect(el().querySelector('a, [data-action="cancel"], [data-action="close"], [data-action="skip"]')).toBeNull();
    const buttons = Array.from(el().querySelectorAll('button'));
    expect(buttons.length).toBe(1);
    expect(buttons[0].getAttribute('type')).toBe('submit');
  });

  it('sends the user back to the login page when opened without a link', () => {
    create(null);

    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does not accept an identifier in place of a link', () => {
    create({ identifier: 'jojo', email: 'jojo' });

    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('shows no errors until the user types, then "Passwords do not match." as Confirm is typed wrong', () => {
    create();
    expect(el().querySelectorAll('[data-error]').length).toBe(0);

    type('newPassword', 'Fresh-P@ss1');
    type('confirmPassword', 'Fresh-P@ss2');

    const errors = Array.from(el().querySelectorAll('[data-error]'));
    expect(errors.length).toBe(1);
    expect(errors[0].getAttribute('data-error')).toBe('confirmPassword');
    expect(errors[0].textContent).toContain('Passwords do not match.');

    type('confirmPassword', 'Fresh-P@ss1');
    expect(el().querySelectorAll('[data-error]').length).toBe(0);
  });

  it('blocks a mismatched or too-short password without calling the API', () => {
    create();
    component.form.setValue({ newPassword: 'abc', confirmPassword: 'abd' });

    component.submit();
    fixture.detectChanges();

    expect(completeFromLogin).not.toHaveBeenCalled();
    expect(component.fieldError('newPassword')).toContain('at least 6');
    expect(component.fieldError('confirmPassword')).toBe('Passwords do not match.');
  });

  it('submits only the token and the new password, then goes to sign in', () => {
    create();
    component.form.setValue({ newPassword: 'Fresh-P@ss1', confirmPassword: 'Fresh-P@ss1' });

    component.submit();

    expect(completeFromLogin).toHaveBeenCalledOnceWith({ token: 'enc-token', newPassword: 'Fresh-P@ss1' });
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
  });

  it('shows one generic message for a refusal and stays on the screen', () => {
    create();
    completeFromLogin.and.returnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    component.form.setValue({ newPassword: 'Fresh-P@ss1', confirmPassword: 'Fresh-P@ss1' });

    component.submit();
    fixture.detectChanges();

    expect(el().querySelector('.login__error')?.textContent).toContain('was not accepted');
    expect(navigate).not.toHaveBeenCalledWith(['/login'], jasmine.anything());
    expect((el().querySelector('[data-action="update-password"]') as HTMLButtonElement).disabled).toBeFalse();
  });

  it('tells the user to wait when they are rate limited', () => {
    create();
    completeFromLogin.and.returnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
    component.form.setValue({ newPassword: 'Fresh-P@ss1', confirmPassword: 'Fresh-P@ss1' });

    component.submit();
    fixture.detectChanges();

    expect(el().querySelector('.login__error')?.textContent).toContain('Too many attempts');
  });
});
