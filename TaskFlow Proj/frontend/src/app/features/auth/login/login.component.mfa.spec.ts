import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthUser } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent two-step sign-in', () => {
  const user: AuthUser = {
    id: 1,
    fullName: 'Jo',
    username: 'jo',
    email: 'jo@taskflow.com',
    role: 'Employee',
    employeeCode: null,
    permissions: []
  };

  let loginSpy: jasmine.Spy;
  let verifySpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  function create(): ComponentFixture<LoginComponent> {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { login: loginSpy, verifyMfa: verifySpy } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
    navigateSpy = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<LoginComponent>): HTMLElement => f.nativeElement as HTMLElement;

  function click(f: ComponentFixture<LoginComponent>, action: string): void {
    (el(f).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    f.detectChanges();
  }

  function passwordStep(f: ComponentFixture<LoginComponent>): void {
    f.componentInstance.form.setValue({ emailOrUsername: 'jo', password: 'secret' });
    f.componentInstance.submit();
    f.detectChanges();
  }

  beforeEach(() => {
    loginSpy = jasmine.createSpy('login').and.returnValue(of({ mfaRequired: true, challengeToken: 'challenge-1' }));
    verifySpy = jasmine.createSpy('verifyMfa').and.returnValue(of(user));
  });

  it('asks for a code instead of signing in when the account has MFA on', () => {
    const fixture = create();

    passwordStep(fixture);

    expect(el(fixture).querySelector('[data-step="mfa"]')).toBeTruthy();
    expect(el(fixture).querySelector('input[formcontrolname="password"]')).toBeNull();
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('still signs in straight away when MFA is off', () => {
    loginSpy.and.returnValue(of(user));
    const fixture = create();

    passwordStep(fixture);

    expect(el(fixture).querySelector('[data-step="mfa"]')).toBeNull();
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('does not call the API for an empty code', () => {
    const fixture = create();
    passwordStep(fixture);

    click(fixture, 'verify-mfa');

    expect(verifySpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="mfaCode"]')).toBeTruthy();
  });

  it('sends the challenge and code and then navigates', () => {
    const fixture = create();
    passwordStep(fixture);

    fixture.componentInstance.mfaForm.controls.code.setValue(' 123456 ');
    click(fixture, 'verify-mfa');

    expect(verifySpy).toHaveBeenCalledOnceWith('challenge-1', '123456');
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('shows the API message for a wrong code and stays on the code step', () => {
    verifySpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The code is incorrect.' } }))
    );
    const fixture = create();
    passwordStep(fixture);

    fixture.componentInstance.mfaForm.controls.code.setValue('000000');
    click(fixture, 'verify-mfa');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('The code is incorrect.');
    expect(el(fixture).querySelector('[data-step="mfa"]')).toBeTruthy();
    expect(fixture.componentInstance.mfaForm.controls.code.value).toBe('');
  });

  it('shows the lock message when too many wrong codes were entered', () => {
    verifySpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 429, error: { error: 'Too many wrong codes. Try again in a few minutes.' } }))
    );
    const fixture = create();
    passwordStep(fixture);

    fixture.componentInstance.mfaForm.controls.code.setValue('000000');
    click(fixture, 'verify-mfa');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Too many wrong codes');
  });

  it('switches to a backup code and back', () => {
    const fixture = create();
    passwordStep(fixture);

    click(fixture, 'toggle-backup');
    expect(el(fixture).textContent).toContain('Enter one of your backup codes.');
    expect((el(fixture).querySelector('[data-field="mfaCode"]') as HTMLInputElement).placeholder).toBe('XXXXX-XXXXX');

    fixture.componentInstance.mfaForm.controls.code.setValue('ABCDE-FGHIJ');
    click(fixture, 'verify-mfa');
    expect(verifySpy).toHaveBeenCalledOnceWith('challenge-1', 'ABCDE-FGHIJ');

    click(fixture, 'toggle-backup');
    expect(el(fixture).textContent).toContain('code from your authenticator app');
  });

  it('goes back to the password step and clears the password', () => {
    const fixture = create();
    passwordStep(fixture);

    click(fixture, 'cancel-mfa');

    expect(el(fixture).querySelector('[data-step="mfa"]')).toBeNull();
    expect(el(fixture).querySelector('input[formcontrolname="password"]')).toBeTruthy();
    expect(fixture.componentInstance.form.controls.password.value).toBe('');
  });
});
