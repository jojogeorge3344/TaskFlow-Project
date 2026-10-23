import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { dashboardRouteFor } from './role-routes';

/** Lets a signed-in user through. Anyone else goes to the login screen, which brings them back to the page they
 * asked for (for example the reset link in an administrator's mail) once they have signed in. */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.currentUser() !== null) {
    return true;
  }

  const returnUrl = state?.url;
  return router.createUrlTree(['/login'], returnUrl && returnUrl !== '/' ? { queryParams: { returnUrl } } : {});
};

/** Keeps a user who has not set up two-step verification on the setup screen. Everything else in the app waits until they have. */
export const mfaSetupGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.currentUser()?.mfaSetupRequired ? router.createUrlTree(['/security-setup']) : true;
};

/** The setup screen is only for people who still have to use it; anyone else goes to their dashboard. */
export const securitySetupGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const user = authService.currentUser();

  if (user === null) {
    return router.createUrlTree(['/login']);
  }

  return user.mfaSetupRequired ? true : router.createUrlTree([dashboardRouteFor(user.role)]);
};

/**
 * Allows the route when the user's role holds at least one of the permissions in `data.permissions`; otherwise sends the
 * user to their own dashboard. This only hides screens: the API checks every request again.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const required = (route.data['permissions'] as string[] | undefined) ?? [];
  const user = authService.currentUser();

  if (user === null) {
    return router.createUrlTree(['/login']);
  }

  return required.some((key) => user.permissions.includes(key))
    ? true
    : router.createUrlTree([dashboardRouteFor(user.role)]);
};
