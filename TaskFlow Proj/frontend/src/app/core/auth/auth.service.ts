import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { AuthResponse, AuthUser, LoginRequest } from './auth.models';
import { TokenStore } from './token.store';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStore = inject(TokenStore);

  private readonly baseUrl = '/api/v1/auth';
  private readonly currentUserSignal = signal<AuthUser | null>(null);
  private refreshInFlight$: Observable<AuthResponse> | null = null;

  readonly currentUser = this.currentUserSignal.asReadonly();

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, request)
      .pipe(
        tap((response) => this.applySession(response)),
        map((response) => response.user)
      );
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

  getAccessToken(): string | null {
    return this.tokenStore.token;
  }

  clearSession(): void {
    this.tokenStore.clear();
    this.currentUserSignal.set(null);
  }

  private applySession(response: AuthResponse): void {
    this.tokenStore.set(response.accessToken);
    this.currentUserSignal.set(response.user);
  }
}
