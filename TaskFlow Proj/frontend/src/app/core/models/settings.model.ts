import { ReminderFrequency, TaskPriority } from './task.model';

/** The global reminder configuration (Screen 13). `timezone` is read-only here; Screen 20 owns it. */
export interface ReminderSettings {
  enabled: boolean;
  deadlineDayEnabled: boolean;
  defaultFirstDays: number;
  defaultSecondDays: number;
  defaultOverdueFrequency: ReminderFrequency;
  mailEnabled: boolean;
  timezone: string;
}

/** A save of the reminder settings; the timezone cannot be changed from this screen. */
export type UpdateReminderSettings = Omit<ReminderSettings, 'timezone'>;

/** The application-level configuration (Screen 20). Non-secret: readable by every signed-in user. */
export interface ApplicationSettings {
  name: string;
  timezone: string;
  defaultPriority: TaskPriority;
  inAppNotificationsEnabled: boolean;
}

/** A save of the application settings; Administrator only. */
export type UpdateApplicationSettings = ApplicationSettings;
