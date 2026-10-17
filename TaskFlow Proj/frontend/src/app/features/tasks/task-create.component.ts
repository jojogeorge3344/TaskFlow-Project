import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { CreateTaskRequest, ReminderFrequency, TASK_PRIORITIES, TaskPriority, User } from '../../core/models';
import { ReminderSettingsService } from '../../core/services/reminder-settings.service';
import { UserService } from '../../core/services/user.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';
import {
  FIRST_REMINDER_OPTIONS,
  OVERDUE_FREQUENCY_OPTIONS,
  ReminderDayOption,
  SECOND_REMINDER_OPTIONS,
  withReminderDay
} from './reminder-options';
import { TaskService } from './task.service';

export const TITLE_MAX_LENGTH = 200;
export const DESCRIPTION_MAX_LENGTH = 4000;

/** Required, ignoring surrounding whitespace. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Mirrors the server rule: the deadline must be later than the start date. */
function deadlineAfterStart(group: AbstractControl): ValidationErrors | null {
  const { startDate, deadline } = (group as FormGroup).getRawValue() as { startDate: string; deadline: string };
  if (!startDate || !deadline) {
    return null;
  }
  return new Date(deadline).getTime() > new Date(startDate).getTime() ? null : { deadlineNotAfterStart: true };
}

/** `yyyy-MM-ddTHH:mm` in local time, the value format of a datetime-local input. */
export function toLocalInput(date: Date): string {
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

type FieldName = 'title' | 'description' | 'assignedToUserId' | 'startDate' | 'deadline' | 'estimatedHours';

/** Create Task (Screen 8). Manager/Administrator only; validation mirrors CreateTaskRequestValidator. */
@Component({
  selector: 'app-task-create',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconComponent],
  templateUrl: './task-create.component.html',
  styleUrl: './task-create.component.css'
})
export class TaskCreateComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly userService = inject(UserService);
  private readonly reminderSettings = inject(ReminderSettingsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly priorities: TaskPriority[] = TASK_PRIORITIES;
  readonly firstReminderOptions = signal<ReminderDayOption[]>(FIRST_REMINDER_OPTIONS);
  readonly secondReminderOptions = signal<ReminderDayOption[]>(SECOND_REMINDER_OPTIONS);
  readonly overdueFrequencyOptions = OVERDUE_FREQUENCY_OPTIONS;
  readonly titleMaxLength = TITLE_MAX_LENGTH;
  readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;

  readonly users = signal<User[]>([]);
  readonly usersLoading = signal(true);
  readonly usersError = signal(false);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly recipientIds = signal<number[]>([]);
  /** Whether the configured reminder defaults reached the form. Until they do, untouched reminder fields
   * are sent as null so the server applies the configured defaults rather than the built-in ones. */
  readonly reminderDefaultsLoaded = signal(false);
  readonly reminderDefaultsUnavailable = signal(false);

  readonly form = new FormGroup(
    {
      title: new FormControl('', {
        nonNullable: true,
        validators: [requiredText, Validators.maxLength(TITLE_MAX_LENGTH)]
      }),
      description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(DESCRIPTION_MAX_LENGTH)] }),
      assignedToUserId: new FormControl<number | null>(null, { validators: [Validators.required] }),
      priority: new FormControl<TaskPriority>('Medium', { nonNullable: true, validators: [Validators.required] }),
      startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      deadline: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      estimatedHours: new FormControl<number | null>(null, {
        validators: [Validators.required, Validators.min(0.01)]
      }),
      firstReminderDays: new FormControl(2, { nonNullable: true }),
      secondReminderDays: new FormControl(1, { nonNullable: true }),
      overdueReminderFrequency: new FormControl<ReminderFrequency>('Daily', { nonNullable: true })
    },
    { validators: [deadlineAfterStart] }
  );

  private readonly assigneeId = toSignal(this.form.controls.assignedToUserId.valueChanges, { initialValue: null });

  readonly assigneeOptions = computed(() => this.users().filter((user) => user.role === 'Employee'));

  /** Anyone active except the assignee, who is always a recipient anyway. */
  readonly recipientOptions = computed(() => this.users().filter((user) => user.id !== this.assigneeId()));

  ngOnInit(): void {
    const start = new Date();
    start.setSeconds(0, 0);
    const deadline = new Date(start);
    deadline.setDate(deadline.getDate() + 3);
    this.form.patchValue({ startDate: toLocalInput(start), deadline: toLocalInput(deadline) });

    this.loadReminderDefaults();

    this.userService.getAll({ isActive: true }).subscribe({
      next: (users) => {
        this.users.set(users);
        this.usersLoading.set(false);
      },
      error: () => {
        this.usersError.set(true);
        this.usersLoading.set(false);
      }
    });
  }

  /** Pre-fills the reminder fields from the configured defaults (Screen 13). Fields the user already
   * changed are left alone. If the defaults cannot be read, a hint says so and untouched fields are sent
   * as null on submit, so the server still applies the configured defaults. */
  private loadReminderDefaults(): void {
    this.reminderSettings.get().subscribe({
      next: (settings) => {
        const { firstReminderDays, secondReminderDays, overdueReminderFrequency } = this.form.controls;
        if (firstReminderDays.pristine) {
          this.firstReminderOptions.set(withReminderDay(FIRST_REMINDER_OPTIONS, settings.defaultFirstDays));
          firstReminderDays.setValue(settings.defaultFirstDays);
        }
        if (secondReminderDays.pristine) {
          this.secondReminderOptions.set(withReminderDay(SECOND_REMINDER_OPTIONS, settings.defaultSecondDays));
          secondReminderDays.setValue(settings.defaultSecondDays);
        }
        if (overdueReminderFrequency.pristine) {
          overdueReminderFrequency.setValue(settings.defaultOverdueFrequency);
        }
        this.reminderDefaultsLoaded.set(true);
      },
      error: () => this.reminderDefaultsUnavailable.set(true)
    });
  }

  showError(name: FieldName): boolean {
    const control = this.form.controls[name];
    const touched = control.touched || this.submitted();
    if (name === 'deadline') {
      return touched && (control.invalid || this.form.hasError('deadlineNotAfterStart'));
    }
    return touched && control.invalid;
  }

  deadlineError(): string {
    return this.form.controls.deadline.hasError('required') ? 'Deadline is required.' : 'Deadline must be after the start date.';
  }

  estimatedHoursError(): string {
    return this.form.controls.estimatedHours.hasError('required')
      ? 'Estimated hours are required.'
      : 'Estimated hours must be a positive number.';
  }

  isRecipient(userId: number): boolean {
    return this.recipientIds().includes(userId);
  }

  toggleRecipient(userId: number, checked: boolean): void {
    this.recipientIds.update((ids) => (checked ? [...ids, userId] : ids.filter((id) => id !== userId)));
  }

  submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const assigneeId = value.assignedToUserId!;
    const { firstReminderDays, secondReminderDays, overdueReminderFrequency } = this.form.controls;
    const useServerDefault = (control: AbstractControl) => !this.reminderDefaultsLoaded() && control.pristine;
    const request: CreateTaskRequest = {
      title: value.title.trim(),
      description: value.description.trim() || null,
      assignedToUserId: assigneeId,
      priority: value.priority,
      startDate: new Date(value.startDate).toISOString(),
      deadline: new Date(value.deadline).toISOString(),
      estimatedHours: Number(value.estimatedHours),
      additionalRecipientUserIds: this.recipientIds().filter((id) => id !== assigneeId),
      firstReminderDays: useServerDefault(firstReminderDays) ? null : Number(value.firstReminderDays),
      secondReminderDays: useServerDefault(secondReminderDays) ? null : Number(value.secondReminderDays),
      overdueReminderFrequency: useServerDefault(overdueReminderFrequency) ? null : value.overdueReminderFrequency
    };

    this.saving.set(true);
    this.taskService.create(request).subscribe({
      next: (task) => {
        this.saving.set(false);
        this.toast.success(`${task.taskCode} created and assigned to ${task.assignedToName}`);
        void this.router.navigate(['/tasks', task.id]);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.serverError.set(apiErrorMessage(err, 'Could not create the task.'));
      }
    });
  }
}
