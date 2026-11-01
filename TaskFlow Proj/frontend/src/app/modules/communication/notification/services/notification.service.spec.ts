import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationService } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('builds query params from the filter', () => {
    service.list({ type: 'TaskAssigned', isRead: false, page: 2, pageSize: 10 }).subscribe();

    const request = httpMock.expectOne((candidate) => candidate.url === '/api/v1/notifications');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('type')).toBe('TaskAssigned');
    expect(request.request.params.get('isRead')).toBe('false');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush({ items: [], total: 0, page: 2, pageSize: 10 });
  });

  it('omits empty filter values', () => {
    service.list({ type: null, isRead: null }).subscribe();

    const request = httpMock.expectOne('/api/v1/notifications');
    expect(request.request.params.keys().length).toBe(0);
    request.flush({ items: [], total: 0, page: 1, pageSize: 20 });
  });

  it('updates the shared unread count signal', () => {
    service.refreshUnreadCount();

    httpMock.expectOne('/api/v1/notifications/unread-count').flush({ count: 3 });

    expect(service.unreadCount()).toBe(3);
  });

  it('keeps the last count when the refresh fails', () => {
    service.unreadCount.set(4);

    service.refreshUnreadCount();
    httpMock
      .expectOne('/api/v1/notifications/unread-count')
      .flush({}, { status: 500, statusText: 'Server Error' });

    expect(service.unreadCount()).toBe(4);
  });

  it('decreases the count on markRead and resets it on markAllRead', () => {
    service.unreadCount.set(3);

    service.markRead(7).subscribe();
    const read = httpMock.expectOne('/api/v1/notifications/7/read');
    expect(read.request.method).toBe('POST');
    read.flush(null);

    expect(service.unreadCount()).toBe(2);

    service.markAllRead().subscribe();
    httpMock.expectOne('/api/v1/notifications/read-all').flush(null);

    expect(service.unreadCount()).toBe(0);
  });
});
