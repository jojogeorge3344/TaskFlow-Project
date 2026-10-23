import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { LOGIN_REASON_PASSWORD_RESET } from './core/auth/login-reasons';
import { LoginComponent } from './features/auth/login/login.component';
import { AccountRecoveryComponent } from './features/auth/account-recovery/account-recovery.component';

describe('forgot password routing', () => {
  it('serves the authenticator recovery flow at account-recovery without a sign-in', async () => {
    const route = routes.find((candidate) => candidate.path === 'account-recovery');

    expect(route).withContext('account-recovery route').toBeDefined();
    expect(route!.canActivate).toBeUndefined();
    expect(await (route!.loadComponent as () => Promise<unknown>)()).toBe(AccountRecoveryComponent);
  });

  it('no longer serves the old public forgot-password page', () => {
    expect(routes.some((candidate) => candidate.path === 'forgot-password')).toBeFalse();
  });

  it('keeps the administrator landing page for the profile reset link', () => {
    const shell = routes.find((candidate) => candidate.path === '' && candidate.children !== undefined)!;

    expect(shell.children!.some((child) => child.path === 'reset-password')).toBeTrue();
  });
});

describe('LoginComponent forgot password link', () => {
  async function create(reason: string | null) {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(reason ? { reason } : {}) } } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('opens the account recovery flow', async () => {
    const page = await create(null);

    const link = page.querySelector('.login__forgot-link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/account-recovery');
  });

  it('confirms a completed reset', async () => {
    const page = await create(LOGIN_REASON_PASSWORD_RESET);

    expect(page.textContent).toContain('Your password has been reset');
  });
});
