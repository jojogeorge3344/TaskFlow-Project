import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models';
import { PasswordResetService } from '../../core/services/password-reset.service';
import { ProfileService } from '../../core/services/profile.service';
import { PASSWORD_RESET_CONFIRMATION } from '../../shared/ui/password-reset-text';
import { ToastService } from '../../shared/ui/toast.service';
import { ProfileComponent } from './profile.component';

describe('ProfileComponent forgot password', () => {
  const me: User = {
    id: 3,
    employeeCode: 'EMP-3',
    fullName: 'Jo Jo',
    username: 'jojo',
    email: 'jojo@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z'
  };

  it('raises a request for the signed-in account, collects no identifier and shows the confirmation', () => {
    const requestOwn = jasmine.createSpy('requestOwn').and.returnValue(of({ message: 'server text' }));
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: () => of(me) } },
        { provide: PasswordResetService, useValue: { requestOwn } },
        { provide: AuthService, useValue: jasmine.createSpyObj<AuthService>('AuthService', ['clearSession', 'applyProfile']) },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-status="forgot-confirmation"]')).toBeNull();

    (element.querySelector('[data-action="forgot-password"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(requestOwn).toHaveBeenCalledOnceWith();
    expect(element.querySelector('[data-status="forgot-confirmation"]')?.textContent?.trim()).toBe(
      PASSWORD_RESET_CONFIRMATION
    );
    // No identifier input exists for the signed-in user: the account comes from their session.
    expect(element.querySelector('[data-field="forgot-identifier"]')).toBeNull();
  });
});
