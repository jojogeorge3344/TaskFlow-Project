import { ReminderFrequency } from './task.model';

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
