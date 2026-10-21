import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { LOGIN_REASON_SESSION_ENDED } from './login-reasons';

const CREDENTIAL_ENDPOINTS = ['/auth/login', '/auth/refresh', '/auth/complete-password-reset'];
/** The `title` of the 403 problem the API answers with while a temporary password must be replaced. */
export const PASSWORD_CHANGE_REQUIRED_TITLE = 'Password change required';
export const FORCE_PASSWORD_CHANGE_ROUTE = '/force-password-change';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isCredentialEndpoint = CREDENTIAL_ENDPOINTS.some((endpoint) => request.url.includes(endpoint));
  const accessToken = authService.getAccessToken();

  const authorizedRequest =
    accessToken !== null && !isCredentialEndpoint
      ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 403 && isPasswordChangeRequired(error)) {
        // Not a sign-out: the user must reach the change-password step, so they stay signed in.
        void router.navigateByUrl(FORCE_PASSWORD_CHANGE_ROUTE);
        return throwError(() => error);
      }

      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isCredentialEndpoint) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((response) =>
          next(request.clone({ setHeaders: { Authorization: `Bearer ${response.accessToken}` } }))
        ),
        catchError((refreshError: unknown) => {
          authService.clearSession();
          void router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_SESSION_ENDED } });
          return throwError(() => refreshError);
        })
      );
    })
  );
};

function isPasswordChangeRequired(error: HttpErrorResponse): boolean {
  return (error.error as { title?: unknown } | null)?.title === PASSWORD_CHANGE_REQUIRED_TITLE;
}
