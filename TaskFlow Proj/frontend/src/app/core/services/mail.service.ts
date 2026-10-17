import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import {
  MailAttachment,
  MailCompose,
  MailComposeMode,
  MailContact,
  MailFlags,
  MailItemDetail,
  MailListItem,
  MailListQuery,
  MailSummary,
  PagedResult,
  SaveDraftRequest,
  SendMailRequest,
  UnreadCount
} from '../models';

/** Internal mail between TaskFlow users, and the unread Inbox count the Mail menu badge shows. */
@Injectable({ providedIn: 'root' })
export class MailService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/mail';

  /** Unread Inbox mails, shared by the menu badge and the Mail screen. */
  readonly unreadCount = signal(0);

  list(query: MailListQuery): Observable<PagedResult<MailListItem>> {
    let params = new HttpParams().set('folder', query.folder);
    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }
    if (query.page) {
      params = params.set('page', query.page);
    }
    if (query.pageSize) {
      params = params.set('pageSize', query.pageSize);
    }
    return this.http.get<PagedResult<MailListItem>>(this.baseUrl, { params });
  }

  summary(): Observable<MailSummary> {
    return this.http.get<MailSummary>(`${this.baseUrl}/summary`);
  }

  /** Re-reads the badge count. Failures are ignored so the badge never blocks the shell. */
  refreshUnreadCount(): void {
    this.http.get<UnreadCount>(`${this.baseUrl}/unread-count`).subscribe({
      next: (result) => this.unreadCount.set(result.count),
      error: () => undefined
    });
  }

  /** Opens an item with its conversation; the server marks it read. */
  open(itemId: number): Observable<MailItemDetail> {
    return this.http.get<MailItemDetail>(`${this.baseUrl}/items/${itemId}`);
  }

  setFlags(itemId: number, flags: MailFlags): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/items/${itemId}`, flags);
  }

  trash(itemId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/items/${itemId}/trash`, null);
  }

  restore(itemId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/items/${itemId}/restore`, null);
  }

  /** Deletes a trashed mail forever, or discards a draft. */
  deleteForever(itemId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/items/${itemId}`);
  }

  compose(itemId: number, mode: MailComposeMode): Observable<MailCompose> {
    return this.http.get<MailCompose>(`${this.baseUrl}/items/${itemId}/compose`, { params: new HttpParams().set('mode', mode) });
  }

  send(request: SendMailRequest): Observable<MailItemDetail> {
    return this.http.post<MailItemDetail>(`${this.baseUrl}/send`, request).pipe(tap(() => this.refreshUnreadCount()));
  }

  createDraft(request: SaveDraftRequest): Observable<MailItemDetail> {
    return this.http.post<MailItemDetail>(`${this.baseUrl}/drafts`, request);
  }

  updateDraft(messageId: number, request: SaveDraftRequest): Observable<MailItemDetail> {
    return this.http.put<MailItemDetail>(`${this.baseUrl}/drafts/${messageId}`, request);
  }

  contacts(search: string): Observable<MailContact[]> {
    return this.http.get<MailContact[]>(`${this.baseUrl}/contacts`, { params: new HttpParams().set('search', search) });
  }

  uploadAttachment(file: File): Observable<MailAttachment> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<MailAttachment>(`${this.baseUrl}/attachments`, form);
  }

  deleteAttachment(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/attachments/${id}`);
  }

  /** Attachment content. Fetched through HttpClient so the auth interceptor adds the bearer token, then saved
   * by the browser. */
  download(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/attachments/${id}`, { responseType: 'blob' });
  }
}
