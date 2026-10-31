import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { LOGIN_REASON_PASSWORD_RESET } from '../../../../shared/auth/login-reasons';
import { AccountRecoveryComponent } from './account-recovery.component';

describe('AccountRecoveryComponent', () => {
  let verifySpy: jasmine.Spy;
  let resetSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  function create(): ComponentFixture<AccountRecoveryComponent> {
    TestBed.configureTestingModule({
      imports: [AccountRecoveryComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { verifyAuthenticatorRecovery: verifySpy, recoverPassword: resetSpy } }
      ]
    });
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(AccountRecoveryComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<AccountRecoveryComponent>): HTMLElement => f.nativeElement as HTMLElement;

  function click(f: ComponentFixture<AccountRecoveryComponent>, action: string): void {
    (el(f).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    f.detectChanges();
  }

  function reachPasswordStep(f: ComponentFixture<AccountRecoveryComponent>): void {
    f.componentInstance.verifyForm.setValue({ identifier: 'jo@taskflow.com', code: '123456' });
    click(f, 'verify');
  }

  beforeEach(() => {
    verifySpy = jasmine.createSpy('verifyAuthenticatorRecovery').and.returnValue(of({ resetToken: 'reset-1' }));
    resetSpy = jasmine.createSpy('recoverPassword').and.returnValue(of(undefined));
  });

  it('asks only for the account and the authenticator code, with no text message option', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-step="verify"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="identifier"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="code"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="mobileNumber"]')).toBeNull();
    expect(el(fixture).textContent?.toLowerCase()).not.toContain('text message');
  });

  it('needs an identifier and a 6-digit code before calling the API', () => {
    const fixture = create();

    click(fixture, 'verify');
    expect(verifySpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="identifier"]')).toBeTruthy();

    fixture.componentInstance.verifyForm.setValue({ identifier: 'jo', code: '12ab' });
    click(fixture, 'verify');
    expect(verifySpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="code"]')).toBeTruthy();
  });

  it('moves to the new password step after a correct code', () => {
    const fixture = create();
    fixture.componentInstance.verifyForm.setValue({ identifier: ' jo@taskflow.com ', code: '123456' });

    click(fixture, 'verify');

    expect(verifySpy).toHaveBeenCalledOnceWith('jo@taskflow.com', '123456');
    expect(el(fixture).querySelector('[data-step="password"]')).toBeTruthy();
  });

  it('shows the same generic message for any failure, clears the code and stays on the first step', () => {
    verifySpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The details are incorrect, or the code has expired.' } }))
    );
    const fixture = create();
    fixture.componentInstance.verifyForm.setValue({ identifier: 'ghost', code: '123456' });

    click(fixture, 'verify');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('The details are incorrect');
    expect(el(fixture).querySelector('[data-step="verify"]')).toBeTruthy();
    expect(fixture.componentInstance.verifyForm.controls.code.value).toBe('');
  });

  it('does not submit a short or mismatched password', () => {
    const fixture = create();
    reachPasswordStep(fixture);

    fixture.componentInstance.passwordForm.setValue({ newPassword: '123', confirmPassword: '123' });
    click(fixture, 'reset-password');
    expect(resetSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="newPassword"]')).toBeTruthy();

    fixture.componentInstance.passwordForm.setValue({ newPassword: 'N3wP@ssw0rd', confirmPassword: 'different' });
    click(fixture, 'reset-password');
    expect(resetSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="confirmPassword"]')?.textContent).toContain('do not match');
  });

  it('resets the password with the token and goes to sign-in with a success notice', () => {
    const fixture = create();
    reachPasswordStep(fixture);

    fixture.componentInstance.passwordForm.setValue({ newPassword: 'N3wP@ssw0rd', confirmPassword: 'N3wP@ssw0rd' });
    click(fixture, 'reset-password');

    expect(resetSpy).toHaveBeenCalledOnceWith({ resetToken: 'reset-1', newPassword: 'N3wP@ssw0rd', confirmPassword: 'N3wP@ssw0rd' });
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: LOGIN_REASON_PASSWORD_RESET } });
  });

  it('offers to start over only when the reset token was refused', () => {
    const fixture = create();
    reachPasswordStep(fixture);
    fixture.componentInstance.passwordForm.setValue({ newPassword: 'N3wP@ssw0rd', confirmPassword: 'N3wP@ssw0rd' });

    resetSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { errors: { NewPassword: ['Too weak.'] } } }))
    );
    click(fixture, 'reset-password');
    expect(el(fixture).querySelector('[data-action="start-over"]')).toBeNull();

    resetSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'This reset link is no longer valid.' } }))
    );
    click(fixture, 'reset-password');
    expect(el(fixture).querySelector('[data-action="start-over"]')).toBeTruthy();

    click(fixture, 'start-over');
    expect(el(fixture).querySelector('[data-step="verify"]')).toBeTruthy();
  });
});
