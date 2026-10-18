import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ChangePassword, UpdateOwnProfile, User } from '../models';

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
}
