import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ReminderSettings, UpdateReminderSettings } from '../../models/settings.model';

/** Reads and saves the global reminder settings (Screen 13). Needs settings.reminders.manage. */
@Injectable({ providedIn: 'root' })
export class ReminderSettingsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/settings/reminders';

  get(): Observable<ReminderSettings> {
    return this.http.get<ReminderSettings>(this.url);
  }

  save(settings: UpdateReminderSettings): Observable<ReminderSettings> {
    return this.http.put<ReminderSettings>(this.url, settings);
  }
}
