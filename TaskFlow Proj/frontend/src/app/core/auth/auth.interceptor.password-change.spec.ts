import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { TokenStore } from './token.store';

describe('authInterceptor password change required', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let navigateByUrl: jasmine.Spy;
  let navigate: jasmine.Spy;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    navigateByUrl = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    navigate = spyOn(router, 'navigate').and.resolveTo(true);
    TestBed.inject(TokenStore).set('access-token');
  });

  afterEach(() => httpMock.verify());

  it('redirects to the force-password-change route on the "Password change required" 403 without signing out', () => {
    let failed = false;
    http.get('/api/v1/tasks').subscribe({ error: () => (failed = true) });

    httpMock
      .expectOne('/api/v1/tasks')
      .flush({ title: 'Password change required', status: 403 }, { status: 403, statusText: 'Forbidden' });

    expect(failed).toBeTrue();
    expect(navigateByUrl).toHaveBeenCalledWith('/force-password-change');
    expect(TestBed.inject(TokenStore).token).toBe('access-token');
  });

  it('leaves an ordinary 403 alone', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/v1/tasks').flush({ title: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });

    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('still signs out when an ordinary 401 cannot be refreshed', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/v1/tasks').flush({}, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'session-ended' } });
    expect(navigateByUrl).not.toHaveBeenCalled();
  });
});

describe('authInterceptor login-page password update', () => {
  it('treats a 401 from the update endpoint as a refusal: no token, no refresh, no redirect', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()]
    });
    const http = TestBed.inject(HttpClient);
    const httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);
    TestBed.inject(TokenStore).set('stale-token');
    let status = 0;

    http.post('/api/v1/auth/complete-password-reset', {}).subscribe({ error: (error) => (status = error.status) });

    const request = httpMock.expectOne('/api/v1/auth/complete-password-reset');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    httpMock.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
    expect(navigate).not.toHaveBeenCalled();
    httpMock.verify();
  });
});
