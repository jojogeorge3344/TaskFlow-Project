import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { NotificationFilter, NotificationItem, UnreadCount } from '../models/notification.model';
import { PagedResult } from '../../../monitoring/task-management/models/task.model';

/**
 * Reads and marks read the caller's own notifications, and owns the shared unread count the
 * header badge and the Notification Center both render (design D6).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/notifications';

  /** Unread count shared by the header badge and the center. */
  readonly unreadCount = signal(0);

  /** A page of the caller's notifications, newest first, optionally filtered (Screen 12). */
  list(filter: NotificationFilter = {}): Observable<PagedResult<NotificationItem>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<NotificationItem>>(this.baseUrl, { params });
  }

  /** Re-reads the unread count for the header badge. Failures are ignored so a badge never
   * blocks the shell. */
  refreshUnreadCount(): void {
    this.http.get<UnreadCount>(`${this.baseUrl}/unread-count`).subscribe({
      next: (result) => this.unreadCount.set(result.count),
      error: () => undefined
    });
  }

  /** Marks one notification read and decrements the shared count. */
  markRead(id: number): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/${id}/read`, null)
      .pipe(tap(() => this.unreadCount.update((count) => Math.max(0, count - 1))));
  }

  /** Marks every notification read and clears the shared count. */
  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, null).pipe(tap(() => this.unreadCount.set(0)));
  }
}
