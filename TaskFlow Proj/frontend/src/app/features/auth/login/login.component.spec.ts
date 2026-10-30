import { provideHttpClient } from '@angular/common/http';
import { systemPermissions } from '../../../testing/permissions';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let httpMock: HttpTestingController;
  let navigateSpy: jasmine.Spy;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    navigateSpy = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders the TaskFlow login form', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.login__title')?.textContent).toContain('TaskFlow');
    expect(compiled.querySelector('input[formcontrolname="emailOrUsername"]')).toBeTruthy();
    expect(compiled.querySelector('input[formcontrolname="password"]')).toBeTruthy();
  });

  it('does not call the API when required fields are empty', () => {
    component.submit();

    httpMock.expectNone('/api/v1/auth/login');
    expect(component.form.controls.emailOrUsername.touched).toBeTrue();
    expect(component.form.controls.password.touched).toBeTrue();
  });

  it('does not call the API for a username that is not an email address', () => {
    component.form.setValue({ emailOrUsername: 'admin', password: 'secret' });
    component.submit();

    httpMock.expectNone('/api/v1/auth/login');
    expect(component.form.controls.emailOrUsername.hasError('email')).toBeTrue();
  });

  it('logs in and navigates to the dashboard for the returned role', () => {
    component.form.setValue({ emailOrUsername: 'admin@taskflow.com', password: 'secret' });
    component.submit();

    const request = httpMock.expectOne('/api/v1/auth/login');
    request.flush({
      accessToken: 'access-token',
      expiresAt: '2030-01-01T00:00:00Z',
      user: {
        id: 1,
        fullName: 'System Administrator',
        username: 'admin',
        email: 'admin@taskflow.com',
        role: 'Administrator',
        employeeCode: 'ADM-001',
        permissions: systemPermissions('Administrator')
      }
    });

    expect(navigateSpy).toHaveBeenCalledWith('/admin');
  });

  it('shows the generic error when credentials are rejected', () => {
    component.form.setValue({ emailOrUsername: 'admin@taskflow.com', password: 'wrong' });
    component.submit();

    httpMock.expectOne('/api/v1/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(component.errorMessage()).toBe('Invalid email or password.');

    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.login__error')?.textContent)
      .toContain('Invalid email or password.');
  });

  it('shows the server message when the authenticator code is refused', () => {
    component.form.setValue({ emailOrUsername: 'admin@taskflow.com', password: 'secret' });
    component.submit();
    httpMock.expectOne('/api/v1/auth/login').flush({ mfaRequired: true, challengeToken: 'challenge' });

    component.mfaForm.controls.code.setValue('123456');
    component.submitCode();
    httpMock.expectOne('/api/v1/auth/mfa/verify').flush(
      { status: 400, detail: 'The code is incorrect, or the sign-in has expired. Try again or sign in again.' },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(component.errorMessage()).toBe('The code is incorrect, or the sign-in has expired. Try again or sign in again.');
  });

  it('shows no sign-out notice by default', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('.login__notice')).toBeNull();
  });

  it('toggles password visibility', () => {
    expect(component.showPassword()).toBeFalse();

    component.togglePassword();
    expect(component.showPassword()).toBeTrue();

    component.togglePassword();
    expect(component.showPassword()).toBeFalse();
  });

  it('shows the password toggle as an icon button inside the password field', () => {
    const root = fixture.nativeElement as HTMLElement;
    const toggle = root.querySelector('.login__password button[data-action="toggle-password"]') as HTMLButtonElement;

    expect(toggle.querySelector('svg')).toBeTruthy();
    expect(toggle.textContent?.trim()).toBe('');
    expect(toggle.getAttribute('aria-label')).toBe('Show password');

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-label')).toBe('Hide password');
    expect((root.querySelector('input[formcontrolname="password"]') as HTMLInputElement).type).toBe('text');
  });
});

describe('LoginComponent sign-out notice', () => {
  it('explains that the credentials changed when sent here for that reason', async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ reason: 'credentials-changed' }) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.login__notice')?.textContent).toContain(
      'Your username or password was changed'
    );
  });
});
