import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { PasswordResetRequestDetails } from '../../../core/models';
import { ToastService } from '../../../shared/ui/toast.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let validateSpy: jasmine.Spy;
  let generateSpy: jasmine.Spy;

  const details: PasswordResetRequestDetails = {
    user: {
      id: 5,
      fullName: 'Anupama Nair',
      username: 'anupama',
      email: 'anupama@taskflow.com',
      role: 'Employee',
      roleId: 3,
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
      employeeCode: 'E-005',
      projectName: 'Apollo'
    },
    requestCount: 3,
    lastRequestedAt: '2026-10-23T05:00:00Z',
    linkExpiresAt: '2026-10-23T05:05:00Z',
    requests: [
      { id: 3, requestedAt: '2026-10-23T05:00:00Z', expiresAt: '2026-10-23T05:05:00Z', status: 'Pending' },
      { id: 2, requestedAt: '2026-10-22T05:00:00Z', expiresAt: '2026-10-22T05:05:00Z', status: 'Expired' },
      { id: 1, requestedAt: '2026-10-21T05:00:00Z', expiresAt: '2026-10-21T05:05:00Z', status: 'Used' }
    ]
  };

  function create(
    queryParams: Record<string, string>,
    validateResult: Observable<unknown> = throwError(() => ({ status: 400 }))
  ): ComponentFixture<ResetPasswordComponent> {
    validateSpy = jasmine.createSpy('validateResetToken').and.returnValue(validateResult);
    generateSpy = jasmine.createSpy('generateNewPassword').and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { validateResetToken: validateSpy, generateNewPassword: generateSpy } },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });

    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<ResetPasswordComponent>) => fixture.nativeElement as HTMLElement;

  it('validates the link and shows the requesting user with their request count', () => {
    const fixture = create({ token: 'one-time-token', userId: '5' }, of(details));

    expect(validateSpy).toHaveBeenCalledOnceWith('one-time-token');
    expect(el(fixture).textContent).toContain('Anupama Nair');
    expect(el(fixture).textContent).toContain('E-005');
    expect(el(fixture).querySelector('[data-stat="count"]')?.textContent).toContain('3');
  });

  it('keeps the request history minimised until it is expanded', () => {
    const fixture = create({ token: 'one-time-token' }, of(details));
    const toggle = el(fixture).querySelector('[data-action="toggle-history"]') as HTMLButtonElement;

    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(el(fixture).querySelector('[data-region="history"]')).toBeNull();

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(el(fixture).querySelectorAll('[data-region="history"] li').length).toBe(3);

    toggle.click();
    fixture.detectChanges();
    expect(el(fixture).querySelector('[data-region="history"]')).toBeNull();
  });

  it('generates the new password with the link token and shows the confirmation', () => {
    const fixture = create({ token: 'one-time-token' }, of(details));

    (el(fixture).querySelector('[data-action="generate-password"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(generateSpy).toHaveBeenCalledOnceWith('one-time-token');
    expect(el(fixture).querySelector('[data-state="done"]')).not.toBeNull();
    expect(el(fixture).querySelector('[data-action="generate-password"]')).toBeNull();
  });

  it('explains that a used or expired link cannot be used', () => {
    const fixture = create({ token: 'stale-token' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(el(fixture).textContent).toContain('not valid or has expired');
    expect(el(fixture).querySelector('[data-action="generate-password"]')).toBeNull();
  });

  it('does not call the API when the link carries no token', () => {
    const fixture = create({});

    expect(validateSpy).not.toHaveBeenCalled();
    expect(el(fixture).textContent).toContain('not valid or has expired');
  });
});
