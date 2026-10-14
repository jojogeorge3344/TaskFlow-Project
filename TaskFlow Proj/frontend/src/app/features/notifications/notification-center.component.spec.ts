import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificationItem, PagedResult } from '../../core/models';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationCenterComponent } from './notification-center.component';

describe('NotificationCenterComponent', () => {
  let httpMock: HttpTestingController;
  let service: NotificationService;
  let fixture: ComponentFixture<NotificationCenterComponent>;

  function item(overrides: Partial<NotificationItem>): NotificationItem {
    return {
      id: 1,
      type: 'TaskAssigned',
      message: 'A task was assigned to you: Design mockups',
      isRead: false,
      createdAt: '2026-10-14T09:00:00Z',
      taskId: 5,
      taskCode: 'TSK-000005',
      ...overrides
    };
  }

  function page(items: NotificationItem[], total = items.length): PagedResult<NotificationItem> {
    return { items, total, page: 1, pageSize: 20 };
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  /** Flushes the mount-time unread-count and first list request. */
  function initPage(items: NotificationItem[], total = items.length): void {
    httpMock
      .expectOne('/api/v1/notifications/unread-count')
      .flush({ count: items.filter((row) => !row.isRead).length });
    httpMock.expectOne((request) => request.url === '/api/v1/notifications').flush(page(items, total));
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NotificationCenterComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(NotificationCenterComponent);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders type, message, timestamp and a task link for each notification', () => {
    initPage([item({})]);
    fixture.detectChanges();

    const row = el().querySelector('.notifications__item')!;
    expect(row.textContent).toContain('Task assigned');
    expect(row.textContent).toContain('A task was assigned to you: Design mockups');
    expect(row.querySelector('[data-task-link]')?.getAttribute('href')).toBe('/tasks/5');
    expect(row.querySelector('[data-task-link]')?.textContent).toContain('TSK-000005');
    expect(row.querySelector('.notifications__time')?.textContent?.trim()).not.toBe('');
  });

  it('renders a row without a link when the related task is gone', () => {
    initPage([item({ type: 'System', message: 'Welcome', taskId: null, taskCode: null })]);
    fixture.detectChanges();

    expect(el().querySelector('.notifications__item')?.textContent).toContain('Welcome');
    expect(el().querySelector('[data-task-link]')).toBeNull();
  });

  it('applies the type and read-state filters from page one', () => {
    initPage([item({})]);
    const component = fixture.componentInstance;
    component.page.set(3);

    component.setType('Reassigned');
    let request = httpMock.expectOne((candidate) => candidate.url === '/api/v1/notifications');
    expect(request.request.params.get('type')).toBe('Reassigned');
    expect(request.request.params.get('page')).toBe('1');
    request.flush(page([]));

    component.setReadState('unread');
    request = httpMock.expectOne((candidate) => candidate.url === '/api/v1/notifications');
    expect(request.request.params.get('isRead')).toBe('false');
    request.flush(page([]));

    component.resetFilters();
    request = httpMock.expectOne((candidate) => candidate.url === '/api/v1/notifications');
    expect(request.request.params.keys()).toEqual(['page', 'pageSize']);
    request.flush(page([]));
  });

  it('marks one read and updates the shared unread count', () => {
    initPage([item({ id: 7 })]);
    fixture.detectChanges();
    expect(service.unreadCount()).toBe(1);

    (el().querySelector('[data-action="mark-read"]') as HTMLButtonElement).click();

    const request = httpMock.expectOne('/api/v1/notifications/7/read');
    expect(request.request.method).toBe('POST');
    request.flush(null);
    fixture.detectChanges();

    expect(service.unreadCount()).toBe(0);
    expect(el().querySelector('[data-action="mark-read"]')).toBeNull();
  });

  it('marks all read and zeroes the shared unread count', () => {
    initPage([item({ id: 1 }), item({ id: 2 })]);
    fixture.detectChanges();
    expect(service.unreadCount()).toBe(2);

    (el().querySelector('[data-action="mark-all"]') as HTMLButtonElement).click();

    httpMock.expectOne('/api/v1/notifications/read-all').flush(null);
    fixture.detectChanges();

    expect(service.unreadCount()).toBe(0);
    expect(el().querySelectorAll('[data-action="mark-read"]').length).toBe(0);
  });

  it('shows the empty state when there are no notifications', () => {
    initPage([]);
    fixture.detectChanges();

    expect(el().textContent).toContain('No notifications yet');
  });

  it('shows the error state and retries', () => {
    httpMock.expectOne('/api/v1/notifications/unread-count').flush({ count: 0 });
    httpMock
      .expectOne((candidate) => candidate.url === '/api/v1/notifications')
      .flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('Something went wrong');

    el().querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    httpMock.expectOne((candidate) => candidate.url === '/api/v1/notifications').flush(page([item({})]));
    fixture.detectChanges();

    expect(el().querySelectorAll('.notifications__item').length).toBe(1);
  });
});
