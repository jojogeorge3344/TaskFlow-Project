export type NotificationType = 'TaskAssigned' | 'Reassigned' | 'DeadlineReminder' | 'Overdue' | 'System';

export const NOTIFICATION_TYPES: NotificationType[] = [
  'TaskAssigned',
  'Reassigned',
  'DeadlineReminder',
  'Overdue',
  'System'
];

/** A row in the Notification Center (Screen 12). `taskId`/`taskCode` are null when the related
 * task is absent or soft-deleted, so the row renders without a link. */
export interface NotificationItem {
  id: number;
  type: NotificationType;
  message: string;
  isRead: boolean;
  createdAt: string;
  taskId: number | null;
  taskCode: string | null;
}

export interface NotificationFilter {
  type?: NotificationType | null;
  isRead?: boolean | null;
  page?: number;
  pageSize?: number;
}

/** The caller's unread notification count, for the header badge. */
export interface UnreadCount {
  count: number;
}
