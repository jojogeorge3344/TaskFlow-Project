import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { systemPermissions } from '../../../testing/permissions';
import { PASSWORD_RESET_CONFIRMATION } from '../../../shared/ui/password-reset-text';
import { LoginComponent } from './login.component';

describe('LoginComponent forgot password', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let httpMock: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function openDialog(): void {
    (el().querySelector('[data-action="forgot-password"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('shows the Forgot Password link to every visitor', () => {
    expect(el().querySelector('[data-action="forgot-password"]')).toBeTruthy();
  });

  it('blocks an empty submission without calling the API', () => {
    openDialog();

    component.submitForgot();
    fixture.detectChanges();

    httpMock.expectNone('/api/v1/auth/password-reset-requests');
    expect(el().querySelector('[data-status="forgot-confirmation"]')).toBeNull();
    expect(el().textContent).toContain('Email or username is required.');
  });

  it('shows the same confirmation for a known and an unknown identifier', () => {
    const texts: string[] = [];

    for (const outcome of ['known', 'unknown']) {
      openDialog();
      component.forgotForm.setValue({ identifier: outcome });
      component.submitForgot();

      const request = httpMock.expectOne('/api/v1/auth/password-reset-requests');
      expect(request.request.body).toEqual({ identifier: outcome });
      request.flush({ message: 'whatever the server says' });
      fixture.detectChanges();

      texts.push(el().querySelector('[data-status="forgot-confirmation"]')?.textContent?.trim() ?? '');
      component.closeForgot();
      fixture.detectChanges();
    }

    expect(texts[0]).toBe(PASSWORD_RESET_CONFIRMATION);
    expect(texts[1]).toBe(texts[0]);
  });

  it('shows the same confirmation even when the request errors', () => {
    openDialog();
    component.forgotForm.setValue({ identifier: 'anyone' });
    component.submitForgot();

    httpMock.expectOne('/api/v1/auth/password-reset-requests').flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el().querySelector('[data-status="forgot-confirmation"]')?.textContent?.trim()).toBe(PASSWORD_RESET_CONFIRMATION);
  });
});

describe('LoginComponent credential notice', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let httpMock: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;
  const noticeUrl = (request: { url: string }) => request.url === '/api/v1/auth/password-reset-notice';

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function showNotice(): void {
    component.form.setValue({ emailOrUsername: 'jojo', password: 'temp' });
    component.checkNotice();
    httpMock.expectOne(noticeUrl).flush({ noticeAvailable: true });
    fixture.detectChanges();
  }

  it('shows the indicator when a notice is available for the typed identifier', () => {
    expect(el().querySelector('[data-notice="credential"]')).toBeNull();

    showNotice();

    expect(el().querySelector('[data-notice="credential"]')).toBeTruthy();
  });

  it('keeps the indicator after a failed sign-in and removes it after a successful one', () => {
    showNotice();

    component.submit();
    httpMock.expectOne('/api/v1/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();
    expect(el().querySelector('[data-notice="credential"]')).toBeTruthy();

    component.submit();
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
        permissions: systemPermissions('Employee')
      }
    });
    fixture.detectChanges();
    expect(el().querySelector('[data-notice="credential"]')).toBeNull();
  });

  it('shows nothing when the lookup fails', () => {
    component.form.setValue({ emailOrUsername: 'jojo', password: '' });
    component.checkNotice();
    httpMock.expectOne(noticeUrl).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el().querySelector('[data-notice="credential"]')).toBeNull();
  });
});
