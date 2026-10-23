/** Folders the user can open. Inbox, Sent and Drafts hold items; Starred, Important and Trash are views. */
export type MailView = 'Inbox' | 'Starred' | 'Important' | 'Sent' | 'Drafts' | 'Trash' | 'PasswordRequest';
export type MailFolder = 'Inbox' | 'Sent' | 'Drafts';
export type MailComposeMode = 'Reply' | 'ReplyAll' | 'Forward';

/** A person on a mail. The TaskFlow system sender has no user id and an empty email. */
export interface MailAddress {
  userId: number | null;
  name: string;
  email: string;
}

export interface MailAttachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface MailListItem {
  itemId: number;
  messageId: number;
  folder: MailFolder;
  from: MailAddress;
  to: MailAddress[];
  draftTo: string | null;
  subject: string;
  preview: string;
  date: string;
  isRead: boolean;
  isStarred: boolean;
  isImportant: boolean;
  hasAttachments: boolean;
  isTrashed: boolean;
  isSystem: boolean;
}

export interface MailMessage {
  messageId: number;
  from: MailAddress;
  to: MailAddress[];
  cc: MailAddress[];
  subject: string;
  body: string;
  sentAt: string | null;
  isSystem: boolean;
  taskId: number | null;
  attachments: MailAttachment[];
}

/** An opened item with its conversation (oldest first). Drafts carry their raw recipient text. */
export interface MailItemDetail {
  itemId: number;
  folder: MailFolder;
  isRead: boolean;
  isStarred: boolean;
  isImportant: boolean;
  isTrashed: boolean;
  draftTo: string | null;
  draftCc: string | null;
  message: MailMessage;
  thread: MailMessage[];
}

export interface MailSummary {
  inboxUnread: number;
  starredUnread: number;
  importantUnread: number;
  drafts: number;
  passwordRequestUnread: number;
}

export interface MailContact {
  id: number;
  fullName: string;
  email: string;
}

export interface MailCompose {
  mode: MailComposeMode;
  parentItemId: number;
  to: string[];
  cc: string[];
  subject: string;
  body: string;
  forwardedAttachments: MailAttachment[];
}

export interface SendMailRequest {
  to: string[] | null;
  cc: string[] | null;
  subject: string | null;
  body: string | null;
  attachmentIds: number[] | null;
  draftId?: number | null;
  parentItemId?: number | null;
  mode?: MailComposeMode | null;
}

export interface SaveDraftRequest {
  to: string[];
  cc: string[];
  subject: string;
  body: string;
  attachmentIds: number[];
  parentItemId?: number | null;
  mode?: MailComposeMode | null;
}

export interface MailFlags {
  isRead?: boolean;
  isStarred?: boolean;
  isImportant?: boolean;
}

export interface MailListQuery {
  folder: MailView;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Limits mirrored from the server (MailRules) for early, friendly messages; the server stays authoritative. */
export const MAIL_LIMITS = {
  maxAttachments: 5,
  maxAttachmentBytes: 10 * 1024 * 1024,
  maxTotalAttachmentBytes: 25 * 1024 * 1024,
  maxSubjectLength: 200,
  maxBodyLength: 20_000,
  blockedExtensions: ['.exe', '.dll', '.bat', '.cmd', '.com', '.msi', '.ps1', '.sh', '.js', '.vbs', '.scr']
} as const;
