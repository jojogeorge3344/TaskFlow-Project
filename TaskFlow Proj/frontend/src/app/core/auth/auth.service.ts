import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import {
  AuthResponse,
  AuthUser,
  LoginRequest,
  MfaChallenge,
  RecoverPasswordRequest,
  RecoveryTokenResponse
} from './auth.models';
import { TokenStore } from './token.store';
import { PasswordResetRequestDetails } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStore = inject(TokenStore);

  private readonly baseUrl = '/api/v1/auth';
  private readonly currentUserSignal = signal<AuthUser | null>(null);
  private refreshInFlight$: Observable<AuthResponse> | null = null;

  readonly currentUser = this.currentUserSignal.asReadonly();

  /** Whether the signed-in user's role holds the permission. The API still decides every request. */
  hasPermission(key: string): boolean {
    return this.currentUserSignal()?.permissions.includes(key) ?? false;
  }

  /** Signs in. For an account with MFA on there is no session yet: the result is a challenge to complete with
   * `verifyMfa` (the type guard `isMfaChallenge` tells the two apart). */
  login(request: LoginRequest): Observable<AuthUser | MfaChallenge> {
    return this.http.post<AuthResponse | MfaChallenge>(`${this.baseUrl}/login`, request).pipe(
      tap((response) => {
        if ('accessToken' in response) {
          this.applySession(response);
        }
      }),
      map((response) => ('accessToken' in response ? response.user : response))
    );
  }

  /** The second sign-in step: the challenge from `login` plus an authenticator code or a backup code. */
  verifyMfa(challengeToken: string, code: string): Observable<AuthUser> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/mfa/verify`, { challengeToken, code }).pipe(
      tap((response) => this.applySession(response)),
      map((response) => response.user)
    );
  }

  /** Account recovery with an authenticator code instead of an SMS code. Returns the single-use reset token. */
  verifyAuthenticatorRecovery(identifier: string, code: string): Observable<RecoveryTokenResponse> {
    return this.http.post<RecoveryTokenResponse>(`${this.baseUrl}/password-recovery/authenticator/verify`, {
      identifier,
      code
    });
  }

  restoreSession(): Observable<AuthUser | null> {
    return this.refresh().pipe(
      map((response) => response.user),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  refresh(): Observable<AuthResponse> {
    if (this.refreshInFlight$ === null) {
      this.refreshInFlight$ = this.http
        .post<AuthResponse>(`${this.baseUrl}/refresh`, {})
        .pipe(
          tap((response) => this.applySession(response)),
          shareReplay({ bufferSize: 1, refCount: false }),
          finalize(() => {
            this.refreshInFlight$ = null;
          })
        );
    }

    return this.refreshInFlight$;
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}).pipe(
      catchError(() => of(undefined)),
      finalize(() => this.clearSession())
    );
  }

  /** Sets the new password with the single-use reset token from `verifyAuthenticatorRecovery`. */
  recoverPassword(request: RecoverPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/password-recovery/reset`, request);
  }

  /** Reads the user behind a one-time reset link. Administrator only; a used or expired link fails. */
  validateResetToken(token: string): Observable<PasswordResetRequestDetails> {
    return this.http.get<PasswordResetRequestDetails>(`${this.baseUrl}/reset-password/${encodeURIComponent(token)}`);
  }

  /** Administrator only. Generates and delivers a new password for the user behind the reset link.
   * The generated password is emailed to the user and never returned here. */
  generateNewPassword(token: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reset-password`, { token });
  }

  /** Administrator-initiated reset from the login page: the system auto-generates a password
   * and notifies the user. No administrator email step. Requires an authenticated Administrator. */
  adminForgotPassword(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/admin-forgot-password`, {});
  }

  /**
   * Asks the API whether the current session is still valid. A 401 (e.g. the user's username or
   * password was changed) is handled by the auth interceptor, which signs the user out.
   */
  checkSession(): Observable<unknown> {
    return this.http.get(`${this.baseUrl}/me`);
  }

  getAccessToken(): string | null {
    return this.tokenStore.token;
  }

  clearSession(): void {
    this.tokenStore.clear();
    this.currentUserSignal.set(null);
  }

  /** Re-syncs the cached user after a self-service profile edit, so the shell shows the new name. */
  applyProfile(user: Omit<AuthUser, 'permissions'>): void {
    // A profile edit never changes the role, so the permissions already held stay as they are.
    this.currentUserSignal.set({ ...user, permissions: this.currentUserSignal()?.permissions ?? [] });
  }

  /** The setup screen finished: the app is open now. The API stopped gating the session as soon as MFA was confirmed. */
  markMfaConfigured(): void {
    const user = this.currentUserSignal();
    if (user) {
      this.currentUserSignal.set({ ...user, mfaSetupRequired: false });
    }
  }

  /** The API refused a request because two-step verification is still to be set up (a stale session); show the setup screen. */
  markMfaRequired(): void {
    const user = this.currentUserSignal();
    if (user) {
      this.currentUserSignal.set({ ...user, mfaSetupRequired: true });
    }
  }

  private applySession(response: AuthResponse): void {
    this.tokenStore.set(response.accessToken);
    this.currentUserSignal.set(response.user);
  }
}
