import { IconName } from '../../../../shared/components/icon/icon.model';
import { MailAddress, MailView } from '../models/mail.model';

export { saveBlob } from '../../../../shared/utils/save-blob';

export interface MailFolderDef {
  view: MailView;
  label: string;
  icon: IconName;
}

/** The folder rail, in display order. */
export const MAIL_FOLDERS: MailFolderDef[] = [
  { view: 'Inbox', label: 'Inbox', icon: 'inbox' },
  { view: 'Starred', label: 'Starred', icon: 'star' },
  { view: 'Important', label: 'Important', icon: 'flag' },
  { view: 'Sent', label: 'Sent', icon: 'send' },
  { view: 'Drafts', label: 'Drafts', icon: 'file' },
  { view: 'Trash', label: 'Trash', icon: 'trash' }
];

/** The folder for a `?folder=` value (case-insensitive), Inbox when missing or unknown. */
export function folderFromParam(value: string | null): MailView {
  return MAIL_FOLDERS.find((folder) => folder.view.toLowerCase() === value?.toLowerCase())?.view ?? 'Inbox';
}

/** Server timestamps are UTC; ones without a zone suffix are read as UTC rather than local time. */
export function parseUtc(iso: string): Date {
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`);
}

/** Time today, otherwise a short date, like a mail list. */
export function listDate(iso: string, now: Date = new Date()): string {
  const date = parseUtc(iso);
  return date.toDateString() === now.toDateString()
    ? date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleDateString([], { day: 'numeric', month: 'short', year: date.getFullYear() === now.getFullYear() ? undefined : 'numeric' });
}

export function fullDate(iso: string | null): string {
  return iso ? parseUtc(iso).toLocaleString() : '';
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function displayName(person: MailAddress): string {
  return person.email ? `${person.name} <${person.email}>` : person.name;
}
