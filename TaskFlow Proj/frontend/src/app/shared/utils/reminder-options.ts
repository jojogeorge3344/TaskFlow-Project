import { ReminderFrequency } from '../../modules/monitoring/task-management/models/task.model';

export interface ReminderDayOption {
  days: number;
  label: string;
}

/** Predefined reminder choices shared by Create Task (Screen 8 FR-9) and Reminder Settings (Screen 13). */
export const FIRST_REMINDER_OPTIONS: ReminderDayOption[] = [
  { days: 3, label: '3 days before the deadline' },
  { days: 2, label: '2 days before the deadline' },
  { days: 1, label: '1 day before the deadline' }
];
export const SECOND_REMINDER_OPTIONS: ReminderDayOption[] = [
  { days: 1, label: '1 day before the deadline' },
  { days: 0, label: 'On the deadline day' }
];
export const OVERDUE_FREQUENCY_OPTIONS: { value: ReminderFrequency; label: string }[] = [
  { value: 'Daily', label: 'Every day' },
  { value: 'Weekly', label: 'Every week' }
];

/** The options plus `days` when it is not one of them (a value saved through the API), so a select
 * can always show the stored value. */
export function withReminderDay(options: ReminderDayOption[], days: number): ReminderDayOption[] {
  if (options.some((option) => option.days === days)) {
    return options;
  }
  const label = days === 0 ? 'On the deadline day' : `${days} ${days === 1 ? 'day' : 'days'} before the deadline`;
  return [...options, { days, label }].sort((a, b) => b.days - a.days);
}
