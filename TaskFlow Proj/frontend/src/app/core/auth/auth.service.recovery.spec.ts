import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService password recovery calls', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sets the new password with the reset token', () => {
    service.recoverPassword({ resetToken: 'abc', newPassword: 'N3wP@ss', confirmPassword: 'N3wP@ss' }).subscribe();

    const request = httpMock.expectOne('/api/v1/auth/password-recovery/reset');
    expect(request.request.body).toEqual({ resetToken: 'abc', newPassword: 'N3wP@ss', confirmPassword: 'N3wP@ss' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

});
