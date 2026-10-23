import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ProfileComponent, REVEAL_UNLOCK_MINUTES, passwordStrength } from './profile.component';

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

describe('ProfileComponent layout', () => {
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
    lastLoginAt: '2026-10-20T08:30:00Z',
    projectName: 'Engineering',
    mfaEnabled: true,
    backupCodesRemaining: 8
  };

  let getSpy: jasmine.Spy;

  function create(): ComponentFixture<ProfileComponent> {
    return createWith({});
  }

  /** Builds the page with the given ProfileService methods replaced. */
  function createWith(overrides: Record<string, jasmine.Spy>): ComponentFixture<ProfileComponent> {
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: getSpy, update: jasmine.createSpy(), changePassword: jasmine.createSpy(), ...overrides } },
        { provide: AuthService, useValue: { clearSession: jasmine.createSpy(), applyProfile: jasmine.createSpy() } },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<ProfileComponent>): HTMLElement => f.nativeElement as HTMLElement;

  function click(f: ComponentFixture<ProfileComponent>, selector: string): void {
    (el(f).querySelector(selector) as HTMLElement).click();
    f.detectChanges();
  }

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(me));
  });

  it('shows who you are in a header with initials and account chips', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.profile-hero__title')?.textContent?.trim()).toBe('Jo Jo');
    expect(el(fixture).querySelector('[data-field="avatar"]')?.textContent?.trim()).toBe('JJ');
    expect(el(fixture).querySelector('.profile-hero__sub')?.textContent).toContain('@jojo');
    expect(el(fixture).querySelector('[data-chip="role"]')?.textContent?.trim()).toBe('Employee');
    expect(el(fixture).querySelector('[data-chip="status"]')?.textContent?.trim()).toBe('Active');
    expect(el(fixture).querySelector('[data-chip="project"]')?.textContent?.trim()).toBe('Engineering');
    expect(el(fixture).querySelector('[data-chip="mfa"]')?.textContent?.trim()).toBe('Two-step on');
    expect(el(fixture).querySelector('[data-chip="last-login"]')).toBeTruthy();
  });

  it('shows the neutral heading while the profile is loading', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('.profile-hero__title')?.textContent?.trim()).toBe('My profile');
  });

  it('switches between the Profile and Security sections with tabs', () => {
    const fixture = create();
    const profilePanel = el(fixture).querySelector('#profile-panel-profile') as HTMLElement;
    const securityPanel = el(fixture).querySelector('#profile-panel-security') as HTMLElement;

    expect(profilePanel.hidden).toBeFalse();
    expect(securityPanel.hidden).toBeTrue();
    expect(el(fixture).querySelector('[data-tab="profile"]')?.getAttribute('aria-selected')).toBe('true');

    click(fixture, '[data-tab="security"]');

    expect(profilePanel.hidden).toBeTrue();
    expect(securityPanel.hidden).toBeFalse();
    expect(el(fixture).querySelector('[data-tab="security"]')?.getAttribute('aria-selected')).toBe('true');
    expect(el(fixture).querySelector('[data-tab="profile"]')?.getAttribute('aria-selected')).toBe('false');
  });

  it('keeps both sections in the page so wide screens can show them side by side', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="fullName"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="currentPassword"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="mfaCode"]')).toBeTruthy();
  });

  it('shows and hides each password field, without asking for a code when two-step verification is off', () => {
    getSpy.and.returnValue(of({ ...me, mfaEnabled: false }));
    const fixture = create();
    const input = (field: string) => el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;

    for (const [field, toggle] of [
      ['currentPassword', 'toggle-current'],
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
    expect(el(fixture).querySelector('[data-section="unlock"]')).toBeNull();
  });

  it('keeps a typed password hidden until an authenticator code unlocks viewing', () => {
    const checkSpy = jasmine.createSpy('checkMfaCode').and.returnValue(of(undefined));
    const fixture = createWith({ checkMfaCode: checkSpy });
    const input = (field: string) => el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;

    click(fixture, '[data-action="toggle-current"]');

    expect(input('currentPassword').type).toBe('password');
    expect(el(fixture).querySelector('[data-section="unlock"]')).toBeTruthy();
    expect(checkSpy).not.toHaveBeenCalled();

    fixture.componentInstance.unlockForm.setValue({ code: '123456' });
    click(fixture, '[data-action="unlock-verify"]');

    expect(checkSpy).toHaveBeenCalledOnceWith('123456');
    expect(input('currentPassword').type).toBe('text');
    expect(el(fixture).querySelector('[data-section="unlock"]')).toBeNull();
    expect(el(fixture).querySelector('[data-state="unlocked"]')).toBeTruthy();

    // Unlocked: the other boxes show without asking again.
    click(fixture, '[data-action="toggle-new"]');
    expect(input('newPassword').type).toBe('text');
    expect(checkSpy).toHaveBeenCalledTimes(1);
  });

  it('refuses a malformed or wrong code and keeps everything hidden', () => {
    const checkSpy = jasmine
      .createSpy('checkMfaCode')
      .and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The code is incorrect or has expired.' } })));
    const fixture = createWith({ checkMfaCode: checkSpy });
    click(fixture, '[data-action="toggle-current"]');

    fixture.componentInstance.unlockForm.setValue({ code: '12ab' });
    click(fixture, '[data-action="unlock-verify"]');
    expect(checkSpy).not.toHaveBeenCalled();

    fixture.componentInstance.unlockForm.setValue({ code: '123456' });
    click(fixture, '[data-action="unlock-verify"]');

    expect(el(fixture).querySelector('[data-error="unlockCode"]')?.textContent).toContain('incorrect or has expired');
    expect((el(fixture).querySelector('[data-field="currentPassword"]') as HTMLInputElement).type).toBe('password');
    expect(fixture.componentInstance.revealUnlocked()).toBeFalse();
  });

  it('can cancel the unlock and lock again, hiding every password', () => {
    const checkSpy = jasmine.createSpy('checkMfaCode').and.returnValue(of(undefined));
    const fixture = createWith({ checkMfaCode: checkSpy });
    click(fixture, '[data-action="toggle-current"]');
    click(fixture, '[data-action="unlock-cancel"]');
    expect(el(fixture).querySelector('[data-section="unlock"]')).toBeNull();

    click(fixture, '[data-action="toggle-current"]');
    fixture.componentInstance.unlockForm.setValue({ code: '123456' });
    click(fixture, '[data-action="unlock-verify"]');
    click(fixture, '[data-action="lock"]');

    expect((el(fixture).querySelector('[data-field="currentPassword"]') as HTMLInputElement).type).toBe('password');
    expect(fixture.componentInstance.revealUnlocked()).toBeFalse();
  });

  it('locks again by itself after the unlock window', fakeAsync(() => {
    const checkSpy = jasmine.createSpy('checkMfaCode').and.returnValue(of(undefined));
    const fixture = createWith({ checkMfaCode: checkSpy });
    click(fixture, '[data-action="toggle-current"]');
    fixture.componentInstance.unlockForm.setValue({ code: '123456' });
    click(fixture, '[data-action="unlock-verify"]');
    expect(fixture.componentInstance.revealUnlocked()).toBeTrue();

    tick(REVEAL_UNLOCK_MINUTES * 60_000);
    fixture.detectChanges();

    expect(fixture.componentInstance.revealUnlocked()).toBeFalse();
    expect((el(fixture).querySelector('[data-field="currentPassword"]') as HTMLInputElement).type).toBe('password');
  }));

  it('asks for an authenticator code on the password form and sends it with the change', () => {
    const changeSpy = jasmine.createSpy('changePassword').and.returnValue(of(undefined));
    const fixture = createWith({ changePassword: changeSpy });
    expect(el(fixture).querySelector('[data-field="mfaCode"]')).toBeTruthy();

    fixture.componentInstance.passwordForm.setValue({
      currentPassword: 'P@ssw0rd!',
      newPassword: 'N3wP@ss!',
      confirmPassword: 'N3wP@ss!',
      mfaCode: ''
    });
    click(fixture, '[data-action="change-password"]');
    expect(changeSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="mfaCode"]')?.textContent).toContain('authenticator');

    fixture.componentInstance.passwordForm.controls.mfaCode.setValue(' 123456 ');
    click(fixture, '[data-action="change-password"]');

    expect(changeSpy).toHaveBeenCalledOnceWith({ currentPassword: 'P@ssw0rd!', newPassword: 'N3wP@ss!', mfaCode: '123456' });
  });

  it('does not ask for a code when two-step verification is off', () => {
    getSpy.and.returnValue(of({ ...me, mfaEnabled: false }));
    const changeSpy = jasmine.createSpy('changePassword').and.returnValue(of(undefined));
    const fixture = createWith({ changePassword: changeSpy });
    expect(el(fixture).querySelector('[data-field="mfaCode"]')).toBeNull();

    fixture.componentInstance.passwordForm.setValue({ currentPassword: 'P@ssw0rd!', newPassword: 'N3wP@ss!', confirmPassword: 'N3wP@ss!', mfaCode: '' });
    click(fixture, '[data-action="change-password"]');

    expect(changeSpy).toHaveBeenCalledOnceWith({ currentPassword: 'P@ssw0rd!', newPassword: 'N3wP@ss!', mfaCode: undefined });
  });

  it('shows the API message when the code is refused', () => {
    const changeSpy = jasmine
      .createSpy('changePassword')
      .and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The code is incorrect or has expired.' } })));
    const fixture = createWith({ changePassword: changeSpy });

    fixture.componentInstance.passwordForm.setValue({ currentPassword: 'P@ssw0rd!', newPassword: 'N3wP@ss!', confirmPassword: 'N3wP@ss!', mfaCode: '000000' });
    click(fixture, '[data-action="change-password"]');

    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('incorrect or has expired');
  });

  it('no longer has a two-step verification card or any way to turn it off', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-section="mfa"]')).toBeNull();
    expect(el(fixture).querySelector('app-mfa-settings')).toBeNull();
    expect(el(fixture).textContent).not.toContain('Turn off');
    expect(el(fixture).textContent).not.toContain('Regenerate');
  });

  it('shows a strength meter that follows the new password', () => {
    const fixture = create();
    const meter = el(fixture).querySelector('[data-field="strength"]') as HTMLElement;
    const type = (value: string) => {
      fixture.componentInstance.passwordForm.controls.newPassword.setValue(value);
      fixture.detectChanges();
    };

    expect(meter.getAttribute('data-level')).toBe('0');

    type('abc');
    expect(meter.getAttribute('data-level')).toBe('1');
    expect(meter.textContent).toContain('Weak');

    type('Abcdefgh1!j');
    expect(meter.getAttribute('data-level')).toBe('4');
    expect(meter.textContent).toContain('Strong');
  });

  it('no longer offers the administrator password reset request', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-action="forgot-password"]')).toBeNull();
    expect(el(fixture).textContent).not.toContain('Locked out');
    expect(el(fixture).querySelector('.profile-reset-notice')).toBeNull();
  });

  it('edits the mobile number with the other personal details and saves it cleaned up', () => {
    const updateSpy = jasmine.createSpy('update').and.returnValue(of({ ...me, mobileNumber: '+919876543210' }));
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: getSpy, update: updateSpy, changePassword: jasmine.createSpy() } },
        { provide: AuthService, useValue: { clearSession: jasmine.createSpy(), applyProfile: jasmine.createSpy() } },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();

    fixture.componentInstance.form.controls.mobileNumber.setValue('+91 98765-43210');
    click(fixture, '[data-action="save"]');

    expect(updateSpy).toHaveBeenCalledOnceWith({ fullName: 'Jo Jo', email: 'jojo@taskflow.com', mobileNumber: '+919876543210' });
  });

  it('refuses a malformed mobile number without calling the API', () => {
    const updateSpy = jasmine.createSpy('update');
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: getSpy, update: updateSpy, changePassword: jasmine.createSpy() } },
        { provide: AuthService, useValue: { clearSession: jasmine.createSpy(), applyProfile: jasmine.createSpy() } },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();

    fixture.componentInstance.form.controls.mobileNumber.setValue('12345');
    click(fixture, '[data-action="save"]');

    expect(updateSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="mobileNumber"]')).toBeTruthy();
  });
});
