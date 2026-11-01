import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MailService } from './mail.service';

describe('MailService', () => {
  let service: MailService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(MailService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists a folder with search and paging', () => {
    service.list({ folder: 'Sent', search: ' report ', page: 2, pageSize: 25 }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/v1/mail');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('folder')).toBe('Sent');
    expect(request.request.params.get('search')).toBe('report');
    expect(request.request.params.get('page')).toBe('2');
    request.flush({ items: [], total: 0, page: 2, pageSize: 25 });
  });

  it('keeps the unread count for the badge and ignores failures', () => {
    service.refreshUnreadCount();
    http.expectOne('/api/v1/mail/unread-count').flush({ count: 3 });
    expect(service.unreadCount()).toBe(3);

    service.refreshUnreadCount();
    http.expectOne('/api/v1/mail/unread-count').flush('down', { status: 500, statusText: 'Error' });
    expect(service.unreadCount()).toBe(3);
  });

  it('opens, flags, trashes, restores and deletes items', () => {
    service.open(7).subscribe();
    expect(http.expectOne('/api/v1/mail/items/7').request.method).toBe('GET');

    service.setFlags(7, { isStarred: true }).subscribe();
    const patch = http.expectOne('/api/v1/mail/items/7');
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.body).toEqual({ isStarred: true });

    service.trash(7).subscribe();
    expect(http.expectOne('/api/v1/mail/items/7/trash').request.method).toBe('POST');
    service.restore(7).subscribe();
    expect(http.expectOne('/api/v1/mail/items/7/restore').request.method).toBe('POST');
    service.deleteForever(7).subscribe();
    expect(http.expectOne('/api/v1/mail/items/7').request.method).toBe('DELETE');
  });

  it('sends mail and refreshes the badge afterwards', () => {
    const body = { to: ['a@taskflow.com'], cc: [], subject: 'Hi', body: 'x', attachmentIds: [1] };
    service.send(body).subscribe();

    const send = http.expectOne('/api/v1/mail/send');
    expect(send.request.method).toBe('POST');
    expect(send.request.body).toEqual(body);
    send.flush({});
    http.expectOne('/api/v1/mail/unread-count').flush({ count: 0 });
  });

  it('creates and updates drafts and reads the compose prefill', () => {
    const draft = { to: [], cc: [], subject: 's', body: 'b', attachmentIds: [] };
    service.createDraft(draft).subscribe();
    expect(http.expectOne('/api/v1/mail/drafts').request.method).toBe('POST');
    service.updateDraft(4, draft).subscribe();
    expect(http.expectOne('/api/v1/mail/drafts/4').request.method).toBe('PUT');

    service.compose(9, 'ReplyAll').subscribe();
    const compose = http.expectOne((req) => req.url === '/api/v1/mail/items/9/compose');
    expect(compose.request.params.get('mode')).toBe('ReplyAll');
  });

  it('uploads attachments as multipart and downloads them as blobs', () => {
    const file = new File(['abc'], 'notes.txt', { type: 'text/plain' });
    service.uploadAttachment(file).subscribe();
    const upload = http.expectOne('/api/v1/mail/attachments');
    expect(upload.request.method).toBe('POST');
    expect(upload.request.body instanceof FormData).toBeTrue();
    expect((upload.request.body as FormData).get('file')).toBeTruthy();

    service.download(5).subscribe();
    const download = http.expectOne('/api/v1/mail/attachments/5');
    expect(download.request.responseType).toBe('blob');

    service.deleteAttachment(5).subscribe();
    expect(http.expectOne('/api/v1/mail/attachments/5').request.method).toBe('DELETE');
  });

  it('searches contacts', () => {
    service.contacts('pri').subscribe();
    const request = http.expectOne((req) => req.url === '/api/v1/mail/contacts');
    expect(request.request.params.get('search')).toBe('pri');
  });
});
