import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { IconComponent } from '../../core/layout/icon.component';
import { ReminderFrequency, ReminderSettings } from '../../core/models';
import { ReminderSettingsService } from '../../core/services/reminder-settings.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';
import {
  FIRST_REMINDER_OPTIONS,
  OVERDUE_FREQUENCY_OPTIONS,
  ReminderDayOption,
  SECOND_REMINDER_OPTIONS,
  withReminderDay
} from '../tasks/reminder-options';

/** Mirrors the server rule: the first reminder must come before the second. */
function firstBeforeSecond(group: AbstractControl): ValidationErrors | null {
  const { defaultFirstDays, defaultSecondDays } = (group as FormGroup).getRawValue() as {
    defaultFirstDays: number;
    defaultSecondDays: number;
  };
  return Number(defaultFirstDays) > Number(defaultSecondDays) ? null : { secondNotBeforeFirst: true };
}

type SettingsField = 'defaultFirstDays' | 'defaultSecondDays' | 'defaultOverdueFrequency';

/** Reminder Settings (Screen 13). Needs settings.reminders.manage; validation mirrors
 * UpdateReminderSettingsRequestValidator. */
@Component({
  selector: 'app-reminder-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './reminder-settings.component.html',
  styleUrl: './reminder-settings.component.css'
})
export class ReminderSettingsComponent implements OnInit {
  private readonly settingsService = inject(ReminderSettingsService);
  private readonly toast = inject(ToastService);

  readonly overdueFrequencyOptions = OVERDUE_FREQUENCY_OPTIONS;
  readonly firstReminderOptions = signal<ReminderDayOption[]>(FIRST_REMINDER_OPTIONS);
  readonly secondReminderOptions = signal<ReminderDayOption[]>(SECOND_REMINDER_OPTIONS);

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedAt = signal<Date | null>(null);
  readonly timezone = signal('');

  readonly form = new FormGroup(
    {
      enabled: new FormControl(true, { nonNullable: true }),
      deadlineDayEnabled: new FormControl(true, { nonNullable: true }),
      defaultFirstDays: new FormControl(2, { nonNullable: true }),
      defaultSecondDays: new FormControl(1, { nonNullable: true }),
      defaultOverdueFrequency: new FormControl<ReminderFrequency>('Daily', { nonNullable: true }),
      mailEnabled: new FormControl(true, { nonNullable: true })
    },
    { validators: [firstBeforeSecond] }
  );

  constructor() {
    // A later edit makes the "Saved" confirmation stale.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.savedAt.set(null));
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.settingsService.get().subscribe({
      next: (settings) => {
        this.apply(settings);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the reminder settings.'));
        this.loading.set(false);
      }
    });
  }

  /** The error to show under a field: a server validation message, or the client ordering rule. */
  fieldError(name: SettingsField): string | null {
    const server = this.form.controls[name].getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (name === 'defaultSecondDays' && this.form.hasError('secondNotBeforeFirst')) {
      return 'The first reminder must come earlier than the second reminder.';
    }
    return null;
  }

  save(): void {
    this.saveError.set(null);
    this.savedAt.set(null);
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.settingsService
      .save({
        enabled: value.enabled,
        deadlineDayEnabled: value.deadlineDayEnabled,
        defaultFirstDays: Number(value.defaultFirstDays),
        defaultSecondDays: Number(value.defaultSecondDays),
        defaultOverdueFrequency: value.defaultOverdueFrequency,
        mailEnabled: value.mailEnabled
      })
      .subscribe({
        next: (settings) => {
          this.saving.set(false);
          this.apply(settings);
          this.savedAt.set(new Date());
          this.toast.success('Reminder settings saved');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.applyServerErrors(err);
          this.saveError.set(apiErrorMessage(err, 'Could not save the reminder settings.'));
        }
      });
  }

  private apply(settings: ReminderSettings): void {
    this.firstReminderOptions.set(withReminderDay(FIRST_REMINDER_OPTIONS, settings.defaultFirstDays));
    this.secondReminderOptions.set(withReminderDay(SECOND_REMINDER_OPTIONS, settings.defaultSecondDays));
    this.timezone.set(settings.timezone);
    this.form.reset({
      enabled: settings.enabled,
      deadlineDayEnabled: settings.deadlineDayEnabled,
      defaultFirstDays: settings.defaultFirstDays,
      defaultSecondDays: settings.defaultSecondDays,
      defaultOverdueFrequency: settings.defaultOverdueFrequency,
      mailEnabled: settings.mailEnabled
    });
  }

  /** Maps a validation ProblemDetails `errors` map (PascalCase keys) onto the matching controls. */
  private applyServerErrors(err: unknown): void {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (!errors) {
      return;
    }
    for (const [key, messages] of Object.entries(errors)) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof typeof this.form.controls;
      const control = this.form.controls[name];
      if (control && messages?.length) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
