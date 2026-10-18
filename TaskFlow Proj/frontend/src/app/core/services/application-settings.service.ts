import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApplicationSettings, UpdateApplicationSettings } from '../models';

/** Reads and saves the application-level settings (Screen 20). Any signed-in user can read;
 * only an Administrator can save. */
@Injectable({ providedIn: 'root' })
export class ApplicationSettingsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/settings/application';

  get(): Observable<ApplicationSettings> {
    return this.http.get<ApplicationSettings>(this.url);
  }

  save(settings: UpdateApplicationSettings): Observable<ApplicationSettings> {
    return this.http.put<ApplicationSettings>(this.url, settings);
  }
}
