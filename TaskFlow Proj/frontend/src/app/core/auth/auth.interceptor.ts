import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { LOGIN_REASON_SESSION_ENDED } from './login-reasons';

const CREDENTIAL_ENDPOINTS = ['/auth/login', '/auth/refresh'];

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
      if (error instanceof HttpErrorResponse && error.status === 403 && (error.error as { code?: string } | null)?.code === 'mfa_setup_required') {
        // The session is valid but two-step verification is not set up yet: show the setup screen instead of a failure.
        authService.markMfaRequired();
        void router.navigate(['/security-setup']);
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
