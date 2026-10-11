import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from './auth.models';
import { AuthService } from './auth.service';
import { dashboardRouteFor } from './role-routes';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.currentUser() !== null ? true : router.createUrlTree(['/login']);
};

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const allowedRoles = (route.data['roles'] as UserRole[] | undefined) ?? [];
  const user = authService.currentUser();

  if (user === null) {
    return router.createUrlTree(['/login']);
  }

  return allowedRoles.includes(user.role) ? true : router.createUrlTree([dashboardRouteFor(user.role)]);
};
