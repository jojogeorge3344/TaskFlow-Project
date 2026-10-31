import { MailComposeMode } from '../../core/models';

/** What to open the composer for. */
export type ComposeRequest =
  | { kind: 'new' }
  | { kind: 'reply'; itemId: number; mode: MailComposeMode }
  | { kind: 'draft'; itemId: number };

export type ComposeResult = 'sent' | 'saved' | 'discarded' | 'cancelled';

export type ComposeField = 'To' | 'Cc' | 'Subject' | 'Body' | 'AttachmentIds' | 'Mode' | 'File';
