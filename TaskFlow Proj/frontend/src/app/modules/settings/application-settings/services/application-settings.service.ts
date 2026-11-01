import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApplicationSettings, UpdateApplicationSettings } from '../../models/settings.model';

/** Reads and saves the application-level settings (Screen 20). Any signed-in user can read;
 * only an Administrator can save. */
@Injectable({ providedIn: 'root' })
export class ApplicationSettingsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/settings/application';

  /** Cached brand name, so a save on Screen 20 updates the shell without a page reload. */
  private readonly nameSignal = signal('TaskFlow');
  readonly appName = this.nameSignal.asReadonly();

  get(): Observable<ApplicationSettings> {
    return this.http.get<ApplicationSettings>(this.url).pipe(tap((settings) => this.applyName(settings)));
  }

  save(settings: UpdateApplicationSettings): Observable<ApplicationSettings> {
    return this.http.put<ApplicationSettings>(this.url, settings).pipe(tap((settings) => this.applyName(settings)));
  }

  private applyName(settings: ApplicationSettings): void {
    this.nameSignal.set(settings.name || 'TaskFlow');
  }
}
