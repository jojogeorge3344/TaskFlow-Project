import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let httpMock: HttpTestingController;
  let navigateSpy: jasmine.Spy;

  beforeEach(async () => {
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

  it('logs in and navigates to the dashboard for the returned role', () => {
    component.form.setValue({ emailOrUsername: 'admin@taskflow.local', password: 'secret' });
    component.submit();

    const request = httpMock.expectOne('/api/v1/auth/login');
    request.flush({
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
    });

    expect(navigateSpy).toHaveBeenCalledWith('/admin');
  });

  it('shows the generic error when credentials are rejected', () => {
    component.form.setValue({ emailOrUsername: 'admin', password: 'wrong' });
    component.submit();

    httpMock.expectOne('/api/v1/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(component.errorMessage()).toBe('Invalid email/username or password.');

    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.login__error')?.textContent)
      .toContain('Invalid email/username or password.');
  });

  it('toggles password visibility', () => {
    expect(component.showPassword()).toBeFalse();

    component.togglePassword();
    expect(component.showPassword()).toBeTrue();

    component.togglePassword();
    expect(component.showPassword()).toBeFalse();
  });
});
