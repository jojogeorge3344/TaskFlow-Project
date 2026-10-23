import { of } from 'rxjs';
import { MailItemDetail, MailListItem, MailMessage } from '../../core/models';

/** Builders and a MailService stub shared by the Mail specs. */
export function listItem(overrides: Partial<MailListItem> = {}): MailListItem {
  return {
    itemId: 1,
    messageId: 10,
    folder: 'Inbox',
    from: { userId: 2, name: 'Ravi Manager', email: 'ravi@taskflow.com' },
    to: [{ userId: 3, name: 'Priya', email: 'priya@taskflow.com' }],
    draftTo: null,
    subject: 'Weekly report',
    preview: 'Please review',
    date: '2026-10-17T06:00:00',
    isRead: false,
    isStarred: false,
    isImportant: false,
    hasAttachments: false,
    isTrashed: false,
    isSystem: false,
    ...overrides
  };
}

export function message(overrides: Partial<MailMessage> = {}): MailMessage {
  return {
    messageId: 10,
    from: { userId: 2, name: 'Ravi Manager', email: 'ravi@taskflow.com' },
    to: [{ userId: 3, name: 'Priya', email: 'priya@taskflow.com' }],
    cc: [],
    subject: 'Weekly report',
    body: 'Please review',
    sentAt: '2026-10-17T06:00:00',
    isSystem: false,
    taskId: null,
    attachments: [],
    ...overrides
  };
}

export function detail(overrides: Partial<MailItemDetail> = {}, messageOverrides: Partial<MailMessage> = {}): MailItemDetail {
  const current = message(messageOverrides);
  return {
    itemId: 1,
    folder: 'Inbox',
    isRead: true,
    isStarred: false,
    isImportant: false,
    isTrashed: false,
    draftTo: null,
    draftCc: null,
    message: current,
    thread: [current],
    ...overrides
  };
}

export function mailServiceStub() {
  return {
    unreadCount: { set: jasmine.createSpy('set') },
    list: jasmine.createSpy('list').and.returnValue(of({ items: [listItem()], total: 1, page: 1, pageSize: 25 })),
    summary: jasmine.createSpy('summary').and.returnValue(of({ inboxUnread: 2, starredUnread: 0, importantUnread: 0, drafts: 1, passwordRequestUnread: 0 })),
    refreshUnreadCount: jasmine.createSpy('refreshUnreadCount'),
    open: jasmine.createSpy('open').and.returnValue(of(detail())),
    setFlags: jasmine.createSpy('setFlags').and.returnValue(of(undefined)),
    trash: jasmine.createSpy('trash').and.returnValue(of(undefined)),
    restore: jasmine.createSpy('restore').and.returnValue(of(undefined)),
    deleteForever: jasmine.createSpy('deleteForever').and.returnValue(of(undefined)),
    compose: jasmine.createSpy('compose'),
    send: jasmine.createSpy('send').and.returnValue(of(detail())),
    createDraft: jasmine.createSpy('createDraft').and.returnValue(of(detail())),
    updateDraft: jasmine.createSpy('updateDraft').and.returnValue(of(detail())),
    contacts: jasmine.createSpy('contacts').and.returnValue(of([])),
    uploadAttachment: jasmine.createSpy('uploadAttachment'),
    deleteAttachment: jasmine.createSpy('deleteAttachment').and.returnValue(of(undefined)),
    download: jasmine.createSpy('download').and.returnValue(of(new Blob(['x'])))
  };
}

export type MailServiceStub = ReturnType<typeof mailServiceStub>;
