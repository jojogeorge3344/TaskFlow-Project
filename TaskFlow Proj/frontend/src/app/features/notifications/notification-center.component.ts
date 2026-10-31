import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import {
  NOTIFICATION_TYPES,
  NotificationFilter,
  NotificationItem,
  NotificationType,
  PagedResult
} from '../../core/models';
import { NotificationService } from '../../core/services/notification.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ReadState } from './notification-center.model';

export const PAGE_SIZE = 20;

export const NOTIFICATION_TYPE_LABELS: Record<NotificationType, string> = {
  TaskAssigned: 'Task assigned',
  Reassigned: 'Task reassigned',
  DeadlineReminder: 'Deadline reminder',
  Overdue: 'Overdue',
  System: 'System'
};

/** Notification Center (Screen 12): the caller's own notifications, filterable, paged, and
 * markable as read, with a link to the related task when one is present. */
@Component({
  selector: 'app-notification-center',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './notification-center.component.html',
  styleUrl: './notification-center.component.css'
})
export class NotificationCenterComponent implements OnInit {
  private readonly permissionAuth = inject(AuthService);
  readonly canManage = computed(() => {
    this.permissionAuth.currentUser();
    return this.permissionAuth.hasPermission(Permissions.NotificationsManage);
  });

  private readonly notificationService = inject(NotificationService);

  readonly types = NOTIFICATION_TYPES;
  readonly typeLabels = NOTIFICATION_TYPE_LABELS;

  readonly items = signal<NotificationItem[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly type = signal<NotificationType | ''>('');
  readonly readState = signal<ReadState>('');
  readonly page = signal(1);

  /** Shared with the header bell, so marking read here updates the badge too (design D6). */
  readonly unreadCount = this.notificationService.unreadCount;

  readonly hasFilters = computed(() => this.type() !== '' || this.readState() !== '');

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  readonly rangeLabel = computed(() => {
    const total = this.total();
    if (total === 0) {
      return '0 notifications';
    }
    const first = (this.page() - 1) * PAGE_SIZE + 1;
    const last = Math.min(this.page() * PAGE_SIZE, total);
    return `${first}–${last} of ${total} ${total === 1 ? 'notification' : 'notifications'}`;
  });

  ngOnInit(): void {
    this.notificationService.refreshUnreadCount();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    const requestedPage = this.page();
    const filter: NotificationFilter = { page: requestedPage, pageSize: PAGE_SIZE };
    if (this.type()) {
      filter.type = this.type() as NotificationType;
    }
    if (this.readState() === 'unread') {
      filter.isRead = false;
    } else if (this.readState() === 'read') {
      filter.isRead = true;
    }

    this.notificationService.list(filter).subscribe({
      next: (result: PagedResult<NotificationItem>) => {
        // A filtered reload can leave the current page past the end of the list (for example,
        // marking the last row of a filtered page read). Fall back to the last page that still
        // has rows so the view never shows a stale, impossible page.
        const lastPage = Math.max(1, Math.ceil(result.total / PAGE_SIZE));
        if (requestedPage > lastPage) {
          this.page.set(lastPage);
          if (result.total > 0) {
            this.load();
            return;
          }
        }

        this.items.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load notifications. Check your connection and try again.'));
        this.loading.set(false);
      }
    });
  }

  setType(value: string): void {
    this.type.set(value as NotificationType | '');
    this.reloadFromFirstPage();
  }

  setReadState(value: string): void {
    this.readState.set(value as ReadState);
    this.reloadFromFirstPage();
  }

  resetFilters(): void {
    this.type.set('');
    this.readState.set('');
    this.reloadFromFirstPage();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.pageCount() || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.load();
  }

  markRead(item: NotificationItem): void {
    if (item.isRead) {
      return;
    }

    this.notificationService.markRead(item.id).subscribe({
      next: () => {
        this.items.update((items) =>
          items.map((row) => (row.id === item.id ? { ...row, isRead: true } : row))
        );
        // A read-state filter would otherwise leave the row in the wrong list.
        if (this.readState() !== '') {
          this.load();
        }
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not mark the notification as read.'));
      }
    });
  }

  markAllRead(): void {
    this.notificationService.markAllRead().subscribe({
      next: () => {
        this.items.update((items) => items.map((row) => ({ ...row, isRead: true })));
        if (this.readState() === 'unread') {
          this.load();
        }
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not mark all notifications as read.'));
      }
    });
  }

  date(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  private reloadFromFirstPage(): void {
    this.page.set(1);
    this.load();
  }
}
