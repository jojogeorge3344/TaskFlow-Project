import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProfileService } from './profile.service';
import { UserService } from '../../management/employee-management/services/user.service';

describe('MFA service calls', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts setup with the current password and returns the key and QR uri', () => {
    let key = '';
    TestBed.inject(ProfileService).startMfaSetup('P@ss').subscribe((setup) => (key = setup.key));

    const request = httpMock.expectOne('/api/v1/users/me/mfa/setup');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ currentPassword: 'P@ss' });
    request.flush({ key: 'ABCD', provisioningUri: 'otpauth://totp/x' });

    expect(key).toBe('ABCD');
  });

  it('confirms setup with a code and returns the backup codes', () => {
    let codes: string[] = [];
    TestBed.inject(ProfileService).enableMfa('123456').subscribe((result) => (codes = result.codes));

    const request = httpMock.expectOne('/api/v1/users/me/mfa/enable');
    expect(request.request.body).toEqual({ code: '123456' });
    request.flush({ codes: ['AAAAA-BBBBB'] });

    expect(codes).toEqual(['AAAAA-BBBBB']);
  });

  it('checks a code without spending it', () => {
    let done = false;
    TestBed.inject(ProfileService).checkMfaCode('123456').subscribe(() => (done = true));

    const request = httpMock.expectOne('/api/v1/users/me/mfa/check');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ code: '123456' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBeTrue();
  });

  it('has no call that turns MFA off', () => {
    expect((TestBed.inject(ProfileService) as unknown as Record<string, unknown>)['disableMfa']).toBeUndefined();
  });

  it('lets an administrator reset a user\'s MFA', () => {
    TestBed.inject(UserService).resetMfa(7).subscribe();

    const request = httpMock.expectOne('/api/v1/users/7/mfa/reset');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
