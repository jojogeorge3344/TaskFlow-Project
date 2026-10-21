import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PasswordResetService } from './password-reset.service';

describe('PasswordResetService', () => {
  let service: PasswordResetService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PasswordResetService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the trimmed identifier from the Login Screen', () => {
    service.requestFromLogin('  jojo  ').subscribe();

    const request = httpMock.expectOne('/api/v1/auth/password-reset-requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ identifier: 'jojo' });
    request.flush({ message: 'generic' });
  });

  it('raises a Profile Screen request without sending any identifier', () => {
    service.requestOwn().subscribe();

    const request = httpMock.expectOne('/api/v1/users/me/password-reset-request');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ message: 'generic' });
  });

  it('maps noticeAvailable to the notice signal and clears it', () => {
    expect(service.noticeAvailable()).toBeFalse();

    service.checkNotice(' jojo ').subscribe();
    const request = httpMock.expectOne(
      (candidate) => candidate.url === '/api/v1/auth/password-reset-notice' && candidate.params.get('identifier') === 'jojo'
    );
    request.flush({ noticeAvailable: true });
    expect(service.noticeAvailable()).toBeTrue();

    service.clearNotice();
    expect(service.noticeAvailable()).toBeFalse();
  });

  it('does not call the API for a blank identifier', () => {
    service.checkNotice('   ').subscribe();

    httpMock.expectNone((candidate) => candidate.url === '/api/v1/auth/password-reset-notice');
    expect(service.noticeAvailable()).toBeFalse();
  });

  it('processes with the password the administrator typed', () => {
    service.process('ref-2', 'N3w-P@ss').subscribe();

    const request = httpMock.expectOne('/api/v1/password-reset-requests/ref-2/process');
    expect(request.request.body).toEqual({ password: 'N3w-P@ss' });
    request.flush({ status: 'Completed' });
  });

  it('retries only the notification', () => {
    service.retryNotification('ref-3').subscribe();

    const request = httpMock.expectOne('/api/v1/password-reset-requests/ref-3/retry-notification');
    expect(request.request.method).toBe('POST');
    request.flush({ status: 'Completed' });
  });
});

describe('PasswordResetService login link', () => {
  let service: PasswordResetService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PasswordResetService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('resolves a token into the email and the notice signal', () => {
    service.resolveLink('enc-token').subscribe();

    const request = httpMock.expectOne(
      (candidate) => candidate.url === '/api/v1/auth/password-reset-link' && candidate.params.get('token') === 'enc-token'
    );
    request.flush({ email: 'manager@taskflow.com', noticeAvailable: true });

    expect(service.noticeAvailable()).toBeTrue();
  });

  it('posts the password update with only the token and the new password', () => {
    service.completeFromLogin({ token: 'enc-token', newPassword: 'Fresh-P@ss1' }).subscribe();

    const request = httpMock.expectOne('/api/v1/auth/complete-password-reset');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'enc-token', newPassword: 'Fresh-P@ss1' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('asks for the administrator login link of a request', () => {
    let path = '';
    service.loginLink('ref-9').subscribe((link) => (path = link.path));

    httpMock.expectOne('/api/v1/password-reset-requests/ref-9/login-link').flush({ path: '/login/abc', url: null });

    expect(path).toBe('/login/abc');
  });
});
