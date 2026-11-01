import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MfaBackupCodes, MfaSetup } from '../../../shared/auth/auth.models';
import { ChangePassword, UpdateOwnProfile, User } from '../../management/employee-management/models/user.model';

/** Self-service profile operations (Screen 19): read and update the caller's own details and
 * change the caller's own password. Every call acts only on the authenticated user. */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/users/me';

  get(): Observable<User> {
    return this.http.get<User>(this.url);
  }

  update(request: UpdateOwnProfile): Observable<User> {
    return this.http.put<User>(this.url, request);
  }

  changePassword(request: ChangePassword): Observable<void> {
    return this.http.post<void>(`${this.url}/password`, request);
  }


  /** MFA step 1: after the current password, returns the key and QR-code URI. MFA stays off until `enableMfa`. */
  startMfaSetup(currentPassword: string): Observable<MfaSetup> {
    return this.http.post<MfaSetup>(`${this.url}/mfa/setup`, { currentPassword });
  }

  /** MFA step 2: confirms with the first code from the app. Returns the backup codes, shown once. */
  enableMfa(code: string): Observable<MfaBackupCodes> {
    return this.http.post<MfaBackupCodes>(`${this.url}/mfa/enable`, { code });
  }

  /** Checks an authenticator code without spending it. Unlocks showing a typed password; wrong codes count toward the lockout. */
  checkMfaCode(code: string): Observable<void> {
    return this.http.post<void>(`${this.url}/mfa/check`, { code });
  }
}
