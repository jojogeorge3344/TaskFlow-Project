import { Routes } from '@angular/router';
import { authGuard, securitySetupGuard } from '../../shared/auth/auth.guard';

/** Sign-in, account recovery and first-time security setup. They run outside the shell. */
export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/pages/login.component').then((m) => m.LoginComponent)
  },
  {
    // Forgot password from the sign-in page: authenticator code, then a new password.
    path: 'account-recovery',
    loadComponent: () =>
      import('./account-recovery/pages/account-recovery.component').then((m) => m.AccountRecoveryComponent)
  },
  {
    // First stop for anyone who has not set up two-step verification: their details, then the authenticator app.
    path: 'security-setup',
    canActivate: [authGuard, securitySetupGuard],
    loadComponent: () =>
      import('./security-setup/pages/security-setup.component').then((m) => m.SecuritySetupComponent)
  }
];
