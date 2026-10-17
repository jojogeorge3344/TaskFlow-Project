import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EmailDelivery, EmailSettings, TestEmailResult, UpdateEmailSettings } from '../models';

/** Email delivery configuration, test email and delivery log (Screen 14). Administrator only. */
@Injectable({ providedIn: 'root' })
export class EmailSettingsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/settings/email';

  get(): Observable<EmailSettings> {
    return this.http.get<EmailSettings>(this.url);
  }

  save(settings: UpdateEmailSettings): Observable<EmailSettings> {
    return this.http.put<EmailSettings>(this.url, settings);
  }

  /** Sends a test email with the saved configuration; without an address it goes to the caller's own email. */
  sendTest(toAddress: string | null): Observable<TestEmailResult> {
    return this.http.post<TestEmailResult>(`${this.url}/test`, { toAddress });
  }

  deliveries(take = 20): Observable<EmailDelivery[]> {
    return this.http.get<EmailDelivery[]>(`${this.url}/deliveries`, { params: new HttpParams().set('take', take) });
  }
}
