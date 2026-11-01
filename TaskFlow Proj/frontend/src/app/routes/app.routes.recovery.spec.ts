import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { expandRoutes } from '../shared/testing/routes';
import { LOGIN_REASON_PASSWORD_RESET } from '../shared/auth/login-reasons';
import { LoginComponent } from '../modules/auth/login/pages/login.component';
import { AccountRecoveryComponent } from '../modules/auth/account-recovery/pages/account-recovery.component';

describe('forgot password routing', () => {
  it('serves the authenticator recovery flow at account-recovery without a sign-in', async () => {
    const route = (await expandRoutes(routes)).find((candidate) => candidate.path === 'account-recovery');

    expect(route).withContext('account-recovery route').toBeDefined();
    expect(route!.canActivate).toBeUndefined();
    expect(await (route!.loadComponent as () => Promise<unknown>)()).toBe(AccountRecoveryComponent);
  });

  it('no longer serves the old public forgot-password page', async () => {
    expect((await expandRoutes(routes)).some((candidate) => candidate.path === 'forgot-password')).toBeFalse();
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
