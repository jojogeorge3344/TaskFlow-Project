import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { systemPermissions } from '../../testing/permissions';
import { AuthResponse, isMfaChallenge } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService MFA calls', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const session: AuthResponse = {
    accessToken: 'access-token',
    expiresAt: '2030-01-01T00:00:00Z',
    user: {
      id: 1,
      fullName: 'Jo',
      username: 'jo',
      email: 'jo@taskflow.com',
      role: 'Employee',
      employeeCode: null,
      permissions: systemPermissions('Employee')
    }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('login returns a challenge and creates no session when the account has MFA on', () => {
    let outcome: unknown;
    service.login({ emailOrUsername: 'jo', password: 'secret' }).subscribe((value) => (outcome = value));

    httpMock.expectOne('/api/v1/auth/login').flush({ mfaRequired: true, challengeToken: 'challenge-1' });

    expect(isMfaChallenge(outcome as never)).toBeTrue();
    expect((outcome as { challengeToken: string }).challengeToken).toBe('challenge-1');
    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
  });

  it('login still returns the user and stores the session when MFA is off', () => {
    let outcome: unknown;
    service.login({ emailOrUsername: 'jo', password: 'secret' }).subscribe((value) => (outcome = value));

    httpMock.expectOne('/api/v1/auth/login').flush(session);

    expect(isMfaChallenge(outcome as never)).toBeFalse();
    expect(service.currentUser()?.username).toBe('jo');
    expect(service.getAccessToken()).toBe('access-token');
  });

  it('verifyMfa posts the challenge and code and stores the session', () => {
    let username = '';
    service.verifyMfa('challenge-1', '123456').subscribe((user) => (username = user.username));

    const request = httpMock.expectOne('/api/v1/auth/mfa/verify');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ challengeToken: 'challenge-1', code: '123456' });
    request.flush(session);

    expect(username).toBe('jo');
    expect(service.currentUser()?.id).toBe(1);
    expect(service.getAccessToken()).toBe('access-token');
  });

  it('recovers with an authenticator code and returns the reset token', () => {
    let token = '';
    service.verifyAuthenticatorRecovery('jo', '123456').subscribe((response) => (token = response.resetToken));

    const request = httpMock.expectOne('/api/v1/auth/password-recovery/authenticator/verify');
    expect(request.request.body).toEqual({ identifier: 'jo', code: '123456' });
    request.flush({ resetToken: 'reset-1' });

    expect(token).toBe('reset-1');
  });
});
