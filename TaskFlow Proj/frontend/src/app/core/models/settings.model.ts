import { ReminderFrequency } from './task.model';

/** The global reminder configuration (Screen 13). `timezone` is read-only here; Screen 20 owns it. */
export interface ReminderSettings {
  enabled: boolean;
  deadlineDayEnabled: boolean;
  defaultFirstDays: number;
  defaultSecondDays: number;
  defaultOverdueFrequency: ReminderFrequency;
  emailEnabled: boolean;
  timezone: string;
}

/** A save of the reminder settings; the timezone cannot be changed from this screen. */
export type UpdateReminderSettings = Omit<ReminderSettings, 'timezone'>;

export type SmtpSecurity = 'None' | 'StartTls' | 'SslOnConnect' | 'Auto';

/**
 * The email delivery configuration (Screen 14). There is no password field: the SMTP password lives only
 * in server configuration, and the screen sees just `passwordConfigured`. `emailEnabled` is the same switch
 * as `ReminderSettings.emailEnabled`.
 */
export interface EmailSettings {
  emailEnabled: boolean;
  smtpHost: string;
  smtpPort: number;
  smtpSecurity: SmtpSecurity;
  senderAddress: string;
  senderName: string;
  smtpUsername: string;
  /** Local time of day (HH:mm, application timezone) when reminder emails go out. */
  sendTime: string;
  passwordConfigured: boolean;
  /** Plain-language problems that would stop email from being sent. */
  issues: string[];
  updatedAt: string | null;
  updatedByName: string | null;
}

export type UpdateEmailSettings = Omit<EmailSettings, 'passwordConfigured' | 'issues' | 'updatedAt' | 'updatedByName'>;

export interface TestEmailResult {
  success: boolean;
  message: string;
  deliveryId: number | null;
}

export type EmailKind = 'Reminder' | 'Test';
export type EmailDeliveryStatus = 'Pending' | 'Sent' | 'Failed' | 'Skipped';

/** One row of the email delivery log. */
export interface EmailDelivery {
  id: number;
  kind: EmailKind;
  toAddress: string;
  subject: string;
  taskId: number | null;
  taskTitle: string | null;
  status: EmailDeliveryStatus;
  attempts: number;
  lastError: string | null;
  createdAt: string;
  lastAttemptAt: string | null;
  nextAttemptAt: string | null;
  sentAt: string | null;
}
