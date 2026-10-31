import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { systemPermissions } from '../testing/permissions';
import { AuthResponse, AuthUser } from './auth.models';
import { mfaSetupGuard, securitySetupGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const user = (overrides: Partial<AuthUser> = {}): AuthUser => ({
  id: 1,
  fullName: 'Jo',
  username: 'jo',
  email: 'jo@taskflow.com',
  role: 'Employee',
  employeeCode: null,
  permissions: systemPermissions('Employee'),
  ...overrides
});

describe('two-step setup guards', () => {
  function configure(current: AuthUser | null): void {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: signal(current).asReadonly() } }]
    });
  }

  function run(guard: typeof mfaSetupGuard): boolean | UrlTree {
    return TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as boolean | UrlTree;
  }

  it('keeps a user who still has to set up two-step verification on the setup screen', () => {
    configure(user({ mfaSetupRequired: true }));

    const result = run(mfaSetupGuard);

    expect(result instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/security-setup');
  });

  it('lets everyone else into the app', () => {
    configure(user({ mfaSetupRequired: false }));
    expect(run(mfaSetupGuard)).toBeTrue();

    TestBed.resetTestingModule();
    configure(user());
    expect(run(mfaSetupGuard)).toBeTrue();
  });

  it('only shows the setup screen to someone who still needs it', () => {
    configure(user({ mfaSetupRequired: true }));
    expect(run(securitySetupGuard)).toBeTrue();

    TestBed.resetTestingModule();
    configure(user({ mfaSetupRequired: false }));
    const result = run(securitySetupGuard) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).not.toBe('/security-setup');
  });

  it('sends a signed-out visitor to the login page', () => {
    configure(null);

    const result = run(securitySetupGuard) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login');
  });
});

describe('two-step setup state and the API gate', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let service: AuthService;

  const session = (overrides: Partial<AuthUser> = {}): AuthResponse => ({
    accessToken: 'token',
    expiresAt: '2030-01-01T00:00:00Z',
    user: user(overrides)
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  function signIn(overrides: Partial<AuthUser> = {}): void {
    service.login({ emailOrUsername: 'jo', password: 'x' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(session(overrides));
  }

  it('remembers from sign-in that setup is required and clears it when setup is finished', () => {
    signIn({ mfaSetupRequired: true });
    expect(service.currentUser()?.mfaSetupRequired).toBeTrue();

    service.markMfaConfigured();

    expect(service.currentUser()?.mfaSetupRequired).toBeFalse();
    expect(service.currentUser()?.username).toBe('jo');
  });

  it('does nothing when nobody is signed in', () => {
    service.markMfaConfigured();
    service.markMfaRequired();

    expect(service.currentUser()).toBeNull();
  });

  it('turns a "setup required" refusal from the API into the setup screen instead of an error page', () => {
    signIn({ mfaSetupRequired: false });
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    let failed = false;

    http.get('/api/v1/tasks').subscribe({ error: () => (failed = true) });
    httpMock.expectOne('/api/v1/tasks').flush({ code: 'mfa_setup_required', error: 'Set up two-step verification to continue.' }, { status: 403, statusText: 'Forbidden' });

    expect(failed).toBeTrue();
    expect(service.currentUser()?.mfaSetupRequired).toBeTrue();
    expect(navigate).toHaveBeenCalledWith(['/security-setup']);
  });

  it('leaves an ordinary 403 alone', () => {
    signIn();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    http.get('/api/v1/settings').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/v1/settings').flush({ error: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });

    expect(service.currentUser()?.mfaSetupRequired).toBeFalsy();
    expect(navigate).not.toHaveBeenCalled();
  });
});
