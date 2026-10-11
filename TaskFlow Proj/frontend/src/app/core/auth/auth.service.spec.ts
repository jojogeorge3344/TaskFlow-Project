import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const response: AuthResponse = {
    accessToken: 'access-token',
    expiresAt: '2030-01-01T00:00:00Z',
    user: {
      id: 1,
      fullName: 'System Administrator',
      username: 'admin',
      email: 'admin@taskflow.local',
      role: 'Administrator',
      employeeCode: 'ADM-001'
    }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('login posts credentials and stores the session in memory', () => {
    service.login({ emailOrUsername: 'admin', password: 'secret' }).subscribe();

    const request = httpMock.expectOne('/api/v1/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ emailOrUsername: 'admin', password: 'secret' });

    request.flush(response);

    expect(service.getAccessToken()).toBe('access-token');
    expect(service.currentUser()?.role).toBe('Administrator');
  });

  it('refresh shares a single in-flight request', () => {
    service.refresh().subscribe();
    service.refresh().subscribe();

    const request = httpMock.expectOne('/api/v1/auth/refresh');
    request.flush(response);

    expect(service.getAccessToken()).toBe('access-token');
  });

  it('restoreSession clears the session when refresh is rejected', () => {
    let result = service.currentUser();
    service.restoreSession().subscribe((user) => (result = user));

    const request = httpMock.expectOne('/api/v1/auth/refresh');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
  });

  it('logout posts and clears the session', () => {
    service.login({ emailOrUsername: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(response);

    service.logout().subscribe();
    httpMock.expectOne('/api/v1/auth/logout').flush(null);

    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
  });
});
