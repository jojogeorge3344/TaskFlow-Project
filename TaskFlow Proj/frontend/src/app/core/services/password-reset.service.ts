import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, tap } from 'rxjs';
import {
  CompletePasswordReset,
  PasswordResetConfirmation,
  PasswordResetLinkResolution,
  PasswordResetLoginLink,
  PasswordResetNotice,
  PasswordResetRequest
} from '../models';

/** Admin-assisted password recovery: raising a request (Login or Profile Screen), reading the Login
 * Screen notice, and the administrator's list / process / retry actions. No call here ever returns
 * or accepts a stored password - a temporary one only ever reaches the user's private mail. */
@Injectable({ providedIn: 'root' })
export class PasswordResetService {
  private readonly http = inject(HttpClient);
  private readonly authUrl = '/api/v1/auth';
  private readonly adminUrl = '/api/v1/password-reset-requests';

  private readonly noticeSignal = signal(false);
  /** Whether the identifier last checked on the Login Screen has a credential notice waiting. */
  readonly noticeAvailable = this.noticeSignal.asReadonly();

  /** Forgot Password on the Login Screen. The answer is the same generic text for every identifier. */
  requestFromLogin(identifier: string): Observable<PasswordResetConfirmation> {
    return this.http.post<PasswordResetConfirmation>(`${this.authUrl}/password-reset-requests`, {
      identifier: identifier.trim()
    });
  }

  /** Forgot Password on the Profile Screen: the account is the caller's own, so nothing is sent. */
  requestOwn(): Observable<PasswordResetConfirmation> {
    return this.http.post<PasswordResetConfirmation>('/api/v1/users/me/password-reset-request', {});
  }

  /** Reads the notice for the identifier typed on the Login Screen into {@link noticeAvailable}. */
  checkNotice(identifier: string): Observable<boolean> {
    const trimmed = identifier.trim();
    if (trimmed === '') {
      this.noticeSignal.set(false);
      return of(false);
    }

    return this.http
      .get<PasswordResetNotice>(`${this.authUrl}/password-reset-notice`, { params: { identifier: trimmed } })
      .pipe(
        map((response) => response.noticeAvailable),
        tap((available) => this.noticeSignal.set(available))
      );
  }

  /** Hides the notice, e.g. after a successful sign-in (the server clears it in the same step). */
  clearNotice(): void {
    this.noticeSignal.set(false);
  }

  /** Resolves an encrypted login-link token into the email to pre-fill, and reads the notice for it into
   * {@link noticeAvailable}. A bad, expired or superseded token resolves to nothing, like an unknown account. */
  resolveLink(token: string): Observable<PasswordResetLinkResolution> {
    return this.http
      .get<PasswordResetLinkResolution>(`${this.authUrl}/password-reset-link`, { params: { token } })
      .pipe(tap((resolution) => this.noticeSignal.set(resolution.noticeAvailable)));
  }

  /** The password update behind the bell. A refusal is one generic 401 whatever was wrong. */
  completeFromLogin(request: CompletePasswordReset): Observable<void> {
    return this.http.post<void>(`${this.authUrl}/complete-password-reset`, request);
  }

  /** The administrator's encrypted login link for a completed request, to pass on with the password. */
  loginLink(referenceId: string): Observable<PasswordResetLoginLink> {
    return this.http.get<PasswordResetLoginLink>(`${this.adminUrl}/${referenceId}/login-link`);
  }

  list(): Observable<PasswordResetRequest[]> {
    return this.http.get<PasswordResetRequest[]>(this.adminUrl);
  }

  /** Processes a request with the password the administrator typed (hashed server-side, never returned). */
  process(referenceId: string, password: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`${this.adminUrl}/${referenceId}/process`, { password });
  }

  retryNotification(referenceId: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`${this.adminUrl}/${referenceId}/retry-notification`, {});
  }
}
