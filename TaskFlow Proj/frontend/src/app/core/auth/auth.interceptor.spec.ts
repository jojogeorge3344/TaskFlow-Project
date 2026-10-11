import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { TokenStore } from './token.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tokenStore: TokenStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    tokenStore = TestBed.inject(TokenStore);
  });

  afterEach(() => httpMock.verify());

  it('attaches the bearer token to protected requests', () => {
    tokenStore.set('access-token');

    http.get('/api/v1/tasks').subscribe();

    const request = httpMock.expectOne('/api/v1/tasks');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({});
  });

  it('does not attach the token to credential endpoints', () => {
    tokenStore.set('access-token');

    http.post('/api/v1/auth/login', {}).subscribe();

    const request = httpMock.expectOne('/api/v1/auth/login');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('refreshes once on a 401 and replays the original request', () => {
    tokenStore.set('stale-token');
    let received: unknown = null;

    http.get('/api/v1/tasks').subscribe((value) => (received = value));

    const first = httpMock.expectOne('/api/v1/tasks');
    expect(first.request.headers.get('Authorization')).toBe('Bearer stale-token');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = httpMock.expectOne('/api/v1/auth/refresh');
    refresh.flush({
      accessToken: 'fresh-token',
      expiresAt: '2030-01-01T00:00:00Z',
      user: {
        id: 1,
        fullName: 'Test User',
        username: 'test',
        email: 'test@taskflow.local',
        role: 'Employee',
        employeeCode: null
      }
    });

    const replay = httpMock.expectOne('/api/v1/tasks');
    expect(replay.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    replay.flush({ ok: true });

    expect(received).toEqual({ ok: true });
  });

  it('clears the session and redirects to /login when refresh fails', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    tokenStore.set('stale-token');

    http.get('/api/v1/tasks').subscribe({ error: () => void 0 });

    httpMock.expectOne('/api/v1/tasks').flush({}, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(tokenStore.token).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });
});
