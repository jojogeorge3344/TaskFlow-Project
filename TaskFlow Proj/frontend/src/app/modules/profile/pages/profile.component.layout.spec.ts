import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../shared/auth/auth.service';
import { User } from '../../management/employee-management/models/user.model';
import { ProfileService } from '../services/profile.service';
import { ToastService } from '../../../shared/services/toast.service';
import { ProfileComponent } from './profile.component';
import { NO_ALLOCATIONS, withProject } from '../../../shared/testing/users';

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
    ...withProject(1, 'Engineering'),
    departments: [],
    managers: [],
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
    expect(el(fixture).querySelector('[data-chip="project"]')?.textContent?.trim()).toBe('Engineering 100%');
    expect(el(fixture).querySelector('[data-chip="mfa"]')?.textContent?.trim()).toBe('Two-step on');
    expect(el(fixture).querySelector('[data-chip="last-login"]')).toBeTruthy();
  });

  it('shows the neutral heading while the profile is loading', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('.profile-hero__title')?.textContent?.trim()).toBe('My profile');
  });

  it('puts Account details on the left, and Personal information then Password on the right', () => {
    const fixture = create();
    const left = el(fixture).querySelector('[data-column="left"]')!;
    const right = el(fixture).querySelector('[data-column="right"]')!;

    expect(left.querySelector('.profile-card--account')).toBeTruthy();
    expect(left.querySelector('.profile-card--personal')).toBeNull();
    expect(left.querySelector('.profile-card--password')).toBeNull();

    const rightCards = Array.from(right.children).map((child) => child.className);
    expect(rightCards.length).toBe(2);
    expect(rightCards[0]).toContain('profile-card--personal');
    expect(rightCards[1]).toContain('profile-card--password');
  });

  it('shows the left column before the right in the document, so narrow screens stack Account first', () => {
    const fixture = create();
    const columns = Array.from(el(fixture).querySelectorAll('.profile-layout > .profile-col')).map((col) => col.getAttribute('data-column'));

    expect(columns).toEqual(['left', 'right']);
  });

  it('no longer has a tab switcher, so every section stays reachable', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.profile-tabs')).toBeNull();
    expect(el(fixture).querySelector('[data-tab]')).toBeNull();
    expect(el(fixture).querySelector('[hidden]')).toBeNull();
  });

  it('keeps the email field spanning both columns of the personal card', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.profile-card--personal .tf-field--wide #profile-email')).toBeTruthy();
  });

  it('puts the Password card directly under Personal information with no stretch between them', () => {
    const fixture = create();
    const personal = el(fixture).querySelector('.profile-card--personal') as HTMLElement;
    const password = el(fixture).querySelector('.profile-card--password') as HTMLElement;
    const gap = password.getBoundingClientRect().top - personal.getBoundingClientRect().bottom;

    expect(gap).toBeLessThan(40);
  });

  it('opens change password from a button, with no inline form or code box on Profile', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="fullName"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-action="open-change-password"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="newPassword"]')).toBeNull();
    expect(el(fixture).querySelector('[data-field="mfaCode"]')).toBeNull();
  });

  it('no longer has a two-step verification card or any way to turn it off', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-section="mfa"]')).toBeNull();
    expect(el(fixture).querySelector('app-mfa-settings')).toBeNull();
    expect(el(fixture).textContent).not.toContain('Turn off');
    expect(el(fixture).textContent).not.toContain('Regenerate');
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
