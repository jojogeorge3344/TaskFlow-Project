import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
import { ProfileService } from '../../core/services/profile.service';
import { ChangePasswordComponent, passwordStrength } from './change-password.component';

describe('passwordStrength', () => {
  it('scores nothing for an empty box and Weak for anything under the minimum length', () => {
    expect(passwordStrength('')).toEqual({ level: 0, label: '' });
    expect(passwordStrength('abc')).toEqual({ level: 1, label: 'Weak' });
  });

  it('rises with length and variety', () => {
    expect(passwordStrength('abcdef').level).toBe(1);
    expect(passwordStrength('abcdefghij').level).toBe(2);
    expect(passwordStrength('Abcdefghij').level).toBe(3);
    expect(passwordStrength('Abcdefgh1!j').level).toBe(4);
    expect(passwordStrength('Abcdefgh1!j').label).toBe('Strong');
  });
});

describe('ChangePasswordComponent', () => {
  let changeSpy: jasmine.Spy;
  let checkSpy: jasmine.Spy;
  let authService: jasmine.SpyObj<AuthService>;
  let navigateSpy: jasmine.Spy;

  function create(): ComponentFixture<ChangePasswordComponent> {
    TestBed.configureTestingModule({
      imports: [ChangePasswordComponent],
      providers: [
        { provide: ProfileService, useValue: { changePassword: changeSpy, checkMfaCode: checkSpy } },
        { provide: AuthService, useValue: authService },
        provideRouter([])
      ]
    });
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(ChangePasswordComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<ChangePasswordComponent>): HTMLElement => f.nativeElement as HTMLElement;
  const dialog = (f: ComponentFixture<ChangePasswordComponent>): HTMLDialogElement =>
    el(f).querySelector('dialog') as HTMLDialogElement;

  function click(f: ComponentFixture<ChangePasswordComponent>, selector: string): void {
    (el(f).querySelector(selector) as HTMLElement).click();
    f.detectChanges();
  }

  function fill(f: ComponentFixture<ChangePasswordComponent>, newPassword: string, confirmPassword = newPassword): void {
    f.componentInstance.passwordForm.setValue({ newPassword, confirmPassword });
    f.detectChanges();
  }

  beforeEach(() => {
    changeSpy = jasmine.createSpy('changePassword').and.returnValue(of(undefined));
    checkSpy = jasmine.createSpy('checkMfaCode').and.returnValue(of(undefined));
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession']);
  });

  it('shows only the new password and its confirmation, with a back button to Profile', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="newPassword"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="confirmPassword"]')).toBeTruthy();
    expect(el(fixture).querySelector('form.pcard [data-field="mfaCode"]')).toBeNull();
    expect(el(fixture).querySelector('[data-field="currentPassword"]')).toBeNull();
    expect(el(fixture).querySelector('[data-action="back"]')?.getAttribute('href')).toBe('/profile');
  });

  it('shows and hides each password with the eye icon and never asks for a code', () => {
    const fixture = create();
    const input = (field: string) => el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;

    for (const [field, toggle] of [
      ['newPassword', 'toggle-new'],
      ['confirmPassword', 'toggle-confirm']
    ]) {
      expect(input(field).type).toBe('password');
      click(fixture, `[data-action="${toggle}"]`);
      expect(input(field).type).toBe('text');
      expect(el(fixture).querySelector(`[data-action="${toggle}"]`)?.getAttribute('aria-label')).toBe('Hide password');
      click(fixture, `[data-action="${toggle}"]`);
      expect(input(field).type).toBe('password');
    }

    expect(dialog(fixture).open).toBeFalse();
    expect(checkSpy).not.toHaveBeenCalled();
  });

  it('shows a strength meter that follows the new password', () => {
    const fixture = create();
    const meter = el(fixture).querySelector('[data-field="strength"]') as HTMLElement;

    expect(meter.getAttribute('data-level')).toBe('0');
    fill(fixture, 'abc', '');
    expect(meter.getAttribute('data-level')).toBe('1');
    fill(fixture, 'Abcdefgh1!j', '');
    expect(meter.getAttribute('data-level')).toBe('4');
    expect(meter.textContent).toContain('Strong');
  });

  it('shows every problem and does not open the popup for an invalid form', () => {
    const fixture = create();

    click(fixture, '[data-action="change-password"]');

    expect(dialog(fixture).open).toBeFalse();
    expect(changeSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="newPassword"]')?.textContent).toContain('Required');
    expect(el(fixture).querySelector('[data-error="confirmPassword"]')?.textContent).toContain('Required');
  });

  it('refuses a confirmation that does not match', () => {
    const fixture = create();
    fill(fixture, 'N3wP@ss!', 'different');

    click(fixture, '[data-action="change-password"]');

    expect(dialog(fixture).open).toBeFalse();
    expect(el(fixture).querySelector('[data-error="confirmPassword"]')?.textContent).toContain('do not match');
  });

  it('opens a popup asking only for the code, and sends nothing until it is confirmed', () => {
    const fixture = create();
    fill(fixture, 'N3wP@ss!');

    click(fixture, '[data-action="change-password"]');

    expect(dialog(fixture).open).toBeTrue();
    expect(dialog(fixture).querySelector('[data-field="mfaCode"]')).toBeTruthy();
    expect(changeSpy).not.toHaveBeenCalled();
  });

  it('saves with the code, signs the user out and goes to login', () => {
    const fixture = create();
    fill(fixture, 'N3wP@ss!');
    click(fixture, '[data-action="change-password"]');

    fixture.componentInstance.codeForm.setValue({ code: ' 123456 ' });
    click(fixture, '[data-action="code-confirm"]');

    expect(changeSpy).toHaveBeenCalledOnceWith({ newPassword: 'N3wP@ss!', mfaCode: '123456' });
    expect(authService.clearSession).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
  });

  it('rejects a wrong code: the popup stays open, the message shows, and nobody is signed out', () => {
    changeSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The code is incorrect or has expired.' } }))
    );
    const fixture = create();
    fill(fixture, 'N3wP@ss!');
    click(fixture, '[data-action="change-password"]');

    fixture.componentInstance.codeForm.setValue({ code: '000000' });
    click(fixture, '[data-action="code-confirm"]');

    expect(dialog(fixture).open).toBeTrue();
    expect(el(fixture).querySelector('[data-error="code"]')?.textContent).toContain('incorrect or has expired');
    expect(authService.clearSession).not.toHaveBeenCalled();
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('does not send anything for an empty code', () => {
    const fixture = create();
    fill(fixture, 'N3wP@ss!');
    click(fixture, '[data-action="change-password"]');

    click(fixture, '[data-action="code-confirm"]');

    expect(changeSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="codeRequired"]')).toBeTruthy();
  });

  it('cancelling the popup sends nothing and keeps what was typed', () => {
    const fixture = create();
    fill(fixture, 'N3wP@ss!');
    click(fixture, '[data-action="change-password"]');

    click(fixture, '[data-action="code-cancel"]');

    expect(dialog(fixture).open).toBeFalse();
    expect(changeSpy).not.toHaveBeenCalled();
    expect(fixture.componentInstance.passwordForm.controls.newPassword.value).toBe('N3wP@ss!');
  });

  it('returns to the form when the server rejects the new password itself', () => {
    changeSpy.and.returnValue(
      throwError(
        () => new HttpErrorResponse({ status: 400, error: { errors: { NewPassword: ['Password is too short.'] } } })
      )
    );
    const fixture = create();
    fill(fixture, 'N3wP@ss!');
    click(fixture, '[data-action="change-password"]');
    fixture.componentInstance.codeForm.setValue({ code: '123456' });

    click(fixture, '[data-action="code-confirm"]');

    expect(dialog(fixture).open).toBeFalse();
    expect(el(fixture).querySelector('[data-error="newPassword"]')?.textContent).toContain('too short');
    expect(authService.clearSession).not.toHaveBeenCalled();
  });
});
