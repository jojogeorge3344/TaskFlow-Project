import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { dashboardRouteFor } from './role-routes';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.currentUser() !== null ? true : router.createUrlTree(['/login']);
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
