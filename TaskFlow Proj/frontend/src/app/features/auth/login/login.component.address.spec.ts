import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';

describe('LoginComponent encrypted login address', () => {
  let httpMock: HttpTestingController;

  const noticeUrl = (request: { url: string }) => request.url === '/api/v1/auth/password-reset-notice';
  const linkUrl = (request: { url: string }) => request.url === '/api/v1/auth/password-reset-link';

  function create(params: Record<string, string>): ComponentFixture<LoginComponent> {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}), paramMap: convertToParamMap(params) } }
        }
      ]
    });
    httpMock = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<LoginComponent>) => fixture.nativeElement as HTMLElement;

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('resolves the token, shows the email pre-filled and shows the bell - the address never holds the email', () => {
    const token = 'Q2ZESjhLQUFBQUFB-token_only';
    const fixture = create({ token });

    const request = httpMock.expectOne((candidate) => linkUrl(candidate) && candidate.params.get('token') === token);
    request.flush({ email: 'manager@taskflow.com', noticeAvailable: true });
    fixture.detectChanges();

    expect(token).not.toContain('@');
    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('manager@taskflow.com');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeTruthy();
  });

  it('shows the email but no bell when the notice is not waiting', () => {
    const fixture = create({ token: 'valid-token' });

    httpMock.expectOne(linkUrl).flush({ email: 'manager@taskflow.com', noticeAvailable: false });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('manager@taskflow.com');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeNull();
  });

  it('behaves like plain /login for a token that does not resolve', () => {
    const fixture = create({ token: 'tampered' });

    httpMock.expectOne(linkUrl).flush({ email: null, noticeAvailable: false });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeNull();
    httpMock.expectNone(noticeUrl);
  });

  it('behaves like plain /login when resolving the token fails', () => {
    const fixture = create({ token: 'whatever' });

    httpMock.expectOne(linkUrl).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeNull();
  });

  it('falls back to the remembered identifier when the token does not resolve', () => {
    localStorage.setItem('taskflow.lastIdentifier', 'remembered.user');
    const fixture = create({ token: 'tampered' });

    httpMock.expectOne(linkUrl).flush({ email: null, noticeAvailable: false });
    httpMock
      .expectOne((candidate) => noticeUrl(candidate) && candidate.params.get('identifier') === 'remembered.user')
      .flush({ noticeAvailable: true });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('remembered.user');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeTruthy();
  });

  it('prefers a resolving token over the remembered identifier', () => {
    localStorage.setItem('taskflow.lastIdentifier', 'remembered.user');
    const fixture = create({ token: 'valid-token' });

    httpMock.expectOne(linkUrl).flush({ email: 'from.token@taskflow.com', noticeAvailable: false });

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('from.token@taskflow.com');
    httpMock.expectNone(noticeUrl);
  });

  it('uses the identifier last used on this device when opened at plain /login', () => {
    localStorage.setItem('taskflow.lastIdentifier', 'remembered.user');
    const fixture = create({});

    httpMock
      .expectOne((candidate) => noticeUrl(candidate) && candidate.params.get('identifier') === 'remembered.user')
      .flush({ noticeAvailable: true });
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('remembered.user');
    expect(el(fixture).querySelector('[data-action="notice-bell"]')).toBeTruthy();
  });

  it('does not look anything up at plain /login with nothing remembered', () => {
    create({});

    httpMock.expectNone(noticeUrl);
    httpMock.expectNone(linkUrl);
  });

  it('remembers the identifier after a successful sign-in only', () => {
    const fixture = create({});
    fixture.componentInstance.form.setValue({ emailOrUsername: 'jojo', password: 'wrong' });

    fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(localStorage.getItem('taskflow.lastIdentifier')).toBeNull();

    fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: 'token',
      expiresAt: '2030-01-01T00:00:00Z',
      user: {
        id: 5,
        fullName: 'Jo Jo',
        username: 'jojo',
        email: 'jojo@taskflow.com',
        role: 'Employee',
        employeeCode: 'EMP-5',
        permissions: []
      }
    });
    expect(localStorage.getItem('taskflow.lastIdentifier')).toBe('jojo');
  });

  it('keeps working when storage is blocked', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('blocked');
    spyOn(Storage.prototype, 'setItem').and.throwError('blocked');

    const fixture = create({});

    expect(fixture.componentInstance.form.controls.emailOrUsername.value).toBe('');
    httpMock.expectNone(noticeUrl);
  });
});
