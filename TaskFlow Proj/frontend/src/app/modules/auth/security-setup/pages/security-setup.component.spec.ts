import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthUser } from '../../../../shared/auth/auth.models';
import { AuthService } from '../../../../shared/auth/auth.service';
import { User } from '../../../management/employee-management/models/user.model';
import { ProfileService } from '../../../profile/services/profile.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { SecuritySetupComponent } from './security-setup.component';
import { NO_ALLOCATIONS, withProject } from '../../../../shared/testing/users';

describe('SecuritySetupComponent', () => {
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
    ...withProject(1, 'Engineering'),
    departments: [],
    managers: [{ id: 20, fullName: 'Anupama Nair' }],
    mobileNumber: null
  };

  const sessionUser: AuthUser = {
    id: 3,
    fullName: 'Jo Jo',
    username: 'jojo',
    email: 'jojo@taskflow.com',
    role: 'Employee',
    employeeCode: 'EMP-3',
    permissions: [],
    mfaSetupRequired: true
  };

  let getSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let startSpy: jasmine.Spy;
  let enableSpy: jasmine.Spy;
  let currentUser: ReturnType<typeof signal<AuthUser | null>>;
  let markConfiguredSpy: jasmine.Spy;
  let logoutSpy: jasmine.Spy;
  let navigateByUrlSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;

  function create(): ComponentFixture<SecuritySetupComponent> {
    TestBed.configureTestingModule({
      imports: [SecuritySetupComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: { get: getSpy, update: updateSpy, startMfaSetup: startSpy, enableMfa: enableSpy } },
        {
          provide: AuthService,
          useValue: {
            currentUser: currentUser.asReadonly(),
            applyProfile: jasmine.createSpy('applyProfile'),
            markMfaConfigured: markConfiguredSpy,
            logout: logoutSpy
          }
        },
        { provide: ToastService, useValue: toast }
      ]
    });
    navigateByUrlSpy = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(SecuritySetupComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: ComponentFixture<SecuritySetupComponent>): HTMLElement => f.nativeElement as HTMLElement;

  function click(f: ComponentFixture<SecuritySetupComponent>, action: string): void {
    (el(f).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    f.detectChanges();
  }

  async function settle(f: ComponentFixture<SecuritySetupComponent>): Promise<void> {
    await f.whenStable();
    f.detectChanges();
  }

  async function toAuthenticator(f: ComponentFixture<SecuritySetupComponent>): Promise<void> {
    f.componentInstance.detailsForm.patchValue({ mobileNumber: '+919876543210' });
    click(f, 'save-details');
  }

  async function toScan(f: ComponentFixture<SecuritySetupComponent>): Promise<void> {
    await toAuthenticator(f);
    f.componentInstance.passwordForm.setValue({ currentPassword: 'P@ss' });
    click(f, 'start-authenticator');
    await settle(f);
  }

  async function toCodes(f: ComponentFixture<SecuritySetupComponent>): Promise<void> {
    await toScan(f);
    f.componentInstance.codeForm.setValue({ code: '123456' });
    click(f, 'confirm-authenticator');
  }

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(me));
    updateSpy = jasmine.createSpy('update').and.callFake((request: { fullName: string; email: string; mobileNumber?: string }) =>
      of({ ...me, ...request })
    );
    startSpy = jasmine
      .createSpy('startMfaSetup')
      .and.returnValue(of({ key: 'ABCDEFGHIJKLMNOP', provisioningUri: 'otpauth://totp/TaskFlow:jojo%40taskflow.com?secret=ABCDEFGHIJKLMNOP' }));
    enableSpy = jasmine.createSpy('enableMfa').and.returnValue(of({ codes: ['AAAAA-BBBBB', 'CCCCC-DDDDD'] }));
    currentUser = signal<AuthUser | null>(sessionUser);
    markConfiguredSpy = jasmine.createSpy('markMfaConfigured');
    logoutSpy = jasmine.createSpy('logout').and.returnValue(of(undefined));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  it('starts on the details step with the employee facts and a three-step progress panel', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-step="details"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="avatar"]')?.textContent?.trim()).toBe('JJ');
    expect(el(fixture).querySelector('h2')?.textContent).toContain('Welcome, Jo');
    expect(el(fixture).querySelector('[data-field="fact-Employee code"]')?.textContent?.trim()).toBe('EMP-3');
    expect(el(fixture).querySelector('[data-field="fact-Managers"]')?.textContent?.trim()).toBe('Anupama Nair');
    expect(el(fixture).querySelectorAll('[data-step-id]').length).toBe(3);
    expect(el(fixture).querySelector('[data-step-id="details"]')?.getAttribute('data-state')).toBe('current');
    expect(el(fixture).querySelector('[data-step-id="codes"]')?.getAttribute('data-state')).toBe('todo');
  });

  it('shows a retry when the details cannot be loaded', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('could not load your details');

    getSpy.and.returnValue(of(me));
    click(fixture, 'retry');
    expect(el(fixture).querySelector('[data-step="details"]')).toBeTruthy();
  });

  it('needs a mobile number in international format before it saves', () => {
    const fixture = create();

    click(fixture, 'save-details');
    expect(updateSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="mobileNumber"]')?.textContent).toContain('Enter your mobile number');

    fixture.componentInstance.detailsForm.patchValue({ mobileNumber: '98765' });
    click(fixture, 'save-details');
    expect(updateSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="mobileNumber"]')?.textContent).toContain('international format');
  });

  it('saves the cleaned-up details and moves to the authenticator step', async () => {
    const fixture = create();
    fixture.componentInstance.detailsForm.setValue({ fullName: ' Jo Jo ', email: 'jojo', mobileNumber: '+91 98765-43210' });

    click(fixture, 'save-details');

    expect(updateSpy).toHaveBeenCalledOnceWith({ fullName: 'Jo Jo', email: 'jojo@taskflow.com', mobileNumber: '+919876543210' });
    expect(el(fixture).querySelector('[data-step="authenticator"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-step-id="details"]')?.getAttribute('data-state')).toBe('done');
  });

  it('shows the API message and stays on the details step when the save is refused', () => {
    updateSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'A user with this email and mobile number already exists.' } })));
    const fixture = create();
    fixture.componentInstance.detailsForm.patchValue({ mobileNumber: '+919876543210' });

    click(fixture, 'save-details');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('already exists');
    expect(el(fixture).querySelector('[data-step="details"]')).toBeTruthy();
  });

  it('asks for the password first, then shows the QR code and the grouped key', async () => {
    const fixture = create();
    await toAuthenticator(fixture);

    click(fixture, 'start-authenticator');
    expect(startSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="currentPassword"]')).toBeTruthy();

    fixture.componentInstance.passwordForm.setValue({ currentPassword: 'P@ss' });
    click(fixture, 'start-authenticator');
    await settle(fixture);

    expect(startSpy).toHaveBeenCalledOnceWith('P@ss');
    expect((el(fixture).querySelector('[data-field="qr"]') as HTMLImageElement).src).toMatch(/^data:image\/png/);
    expect(el(fixture).querySelector('[data-field="key"]')?.textContent).toContain('ABCD EFGH IJKL MNOP');
  });

  it('shows the API message when the password is wrong and keeps asking for it', async () => {
    startSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Current password is incorrect.' } })));
    const fixture = create();
    await toAuthenticator(fixture);
    fixture.componentInstance.passwordForm.setValue({ currentPassword: 'wrong' });

    click(fixture, 'start-authenticator');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Current password is incorrect.');
    expect(el(fixture).querySelector('[data-step="authenticator-password"]')).toBeTruthy();
  });

  it('lets the user go back to their details', async () => {
    const fixture = create();
    await toAuthenticator(fixture);

    click(fixture, 'back');

    expect(el(fixture).querySelector('[data-step="details"]')).toBeTruthy();
  });

  it('rejects a malformed or wrong code and stays on the scan step', async () => {
    const fixture = create();
    await toScan(fixture);

    fixture.componentInstance.codeForm.setValue({ code: '12ab' });
    click(fixture, 'confirm-authenticator');
    expect(enableSpy).not.toHaveBeenCalled();

    enableSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The code is incorrect or has expired.' } })));
    fixture.componentInstance.codeForm.setValue({ code: '123456' });
    click(fixture, 'confirm-authenticator');

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('incorrect or has expired');
    expect(el(fixture).querySelector('[data-step="authenticator-scan"]')).toBeTruthy();
  });

  it('can start the authenticator step over', async () => {
    const fixture = create();
    await toScan(fixture);

    click(fixture, 'restart');

    expect(el(fixture).querySelector('[data-step="authenticator-password"]')).toBeTruthy();
    expect(el(fixture).querySelector('[data-field="qr"]')).toBeNull();
  });

  it('shows the backup codes after a correct code and drops the key', async () => {
    const fixture = create();

    await toCodes(fixture);

    expect(enableSpy).toHaveBeenCalledOnceWith('123456');
    expect(el(fixture).querySelector('[data-step="codes"]')?.textContent).toContain('AAAAA-BBBBB');
    expect(el(fixture).querySelector('[data-field="key"]')).toBeNull();
    expect(el(fixture).querySelector('[data-step-id="authenticator"]')?.getAttribute('data-state')).toBe('done');
  });

  it('keeps Open TaskFlow disabled until the codes are marked as saved, then opens the dashboard', async () => {
    const fixture = create();
    await toCodes(fixture);
    const finish = (): HTMLButtonElement => el(fixture).querySelector('[data-action="finish"]') as HTMLButtonElement;

    expect(finish().disabled).toBeTrue();
    fixture.componentInstance.finish();
    expect(markConfiguredSpy).not.toHaveBeenCalled();

    (el(fixture).querySelector('[data-field="saved"]') as HTMLInputElement).click();
    fixture.detectChanges();
    expect(finish().disabled).toBeFalse();

    finish().click();

    expect(markConfiguredSpy).toHaveBeenCalled();
    expect(navigateByUrlSpy).toHaveBeenCalled();
    expect(fixture.componentInstance.backupCodes()).toEqual([]);
  });

  it('copies the backup codes', async () => {
    const write = jasmine.createSpy('writeText').and.resolveTo();
    spyOnProperty(navigator, 'clipboard', 'get').and.returnValue({ writeText: write } as unknown as Clipboard);
    const fixture = create();
    await toCodes(fixture);

    await fixture.componentInstance.copyCodes();

    expect(write).toHaveBeenCalledWith('AAAAA-BBBBB\nCCCCC-DDDDD');
    expect(toast.success).toHaveBeenCalledWith('Backup codes copied');
  });

  it('signs out from the first step', () => {
    const fixture = create();

    click(fixture, 'sign-out');

    expect(logoutSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });
});
