import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { CreateTaskRequest, ProjectWithCount, ReminderFrequency, TASK_PRIORITIES, TaskDetails, TaskPriority, User } from '../../core/models';
import { ApplicationSettingsService } from '../../core/services/application-settings.service';
import { ProjectService } from '../../core/services/project.service';
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

type FieldName = 'title' | 'projectId' | 'description' | 'assignedToUserId' | 'startDate' | 'deadline' | 'estimatedHours';

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
  private readonly applicationSettings = inject(ApplicationSettingsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectService);

  readonly priorities: TaskPriority[] = TASK_PRIORITIES;
  readonly firstReminderOptions = signal<ReminderDayOption[]>(FIRST_REMINDER_OPTIONS);
  readonly secondReminderOptions = signal<ReminderDayOption[]>(SECOND_REMINDER_OPTIONS);
  readonly overdueFrequencyOptions = OVERDUE_FREQUENCY_OPTIONS;
  readonly titleMaxLength = TITLE_MAX_LENGTH;
  readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;

  readonly users = signal<User[]>([]);
  readonly projects = signal<ProjectWithCount[]>([]);
  readonly projectsLoading = signal(true);
  readonly projectsError = signal(false);
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
  /** Set when the form was opened with `?copyFrom=`: the task being copied, once loaded. */
  private readonly copySource = signal<TaskDetails | null>(null);
  /** Tells the user what was (or could not be) carried over from the copied task. */
  readonly copyNotice = signal<{ kind: 'info' | 'warning'; message: string } | null>(null);

  readonly form = new FormGroup(
    {
      title: new FormControl('', {
        nonNullable: true,
        validators: [requiredText, Validators.maxLength(TITLE_MAX_LENGTH)]
      }),
      description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(DESCRIPTION_MAX_LENGTH)] }),
      projectId: new FormControl<number | null>(null, { validators: [Validators.required] }),
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
    this.loadDefaultPriority();
    this.loadCopySource();

    this.projectService.getAll().subscribe({
      next: (projects) => {
        this.projects.set(projects);
        this.projectsLoading.set(false);
        const requested = Number(this.route.snapshot.queryParamMap.get('projectId'));
        if (requested > 0 && projects.some((project) => project.id === requested) && this.form.controls.projectId.pristine) {
          this.form.controls.projectId.setValue(requested);
        }
      },
      error: () => {
        this.projectsError.set(true);
        this.projectsLoading.set(false);
      }
    });

    this.userService.getAll({ isActive: true }).subscribe({
      next: (users) => {
        this.users.set(users);
        this.usersLoading.set(false);
        this.applyCopyPeople();
      },
      error: () => {
        this.usersError.set(true);
        this.usersLoading.set(false);
      }
    });
  }

  /** Pre-fills the form from the task named by `?copyFrom=`. Nothing is saved until the user submits, and the
   * dates keep the new-task defaults so a copy of an old task does not start out overdue. */
  private loadCopySource(): void {
    const copyFrom = Number(this.route.snapshot.queryParamMap.get('copyFrom'));
    if (!Number.isInteger(copyFrom) || copyFrom <= 0) {
      return;
    }

    this.taskService.getById(copyFrom).subscribe({
      next: (source) => {
        const { projectId, priority, firstReminderDays, secondReminderDays, overdueReminderFrequency } = this.form.controls;
        this.form.patchValue({
          title: source.title,
          description: source.description ?? '',
          priority: source.priority,
          estimatedHours: source.estimatedHours,
          firstReminderDays: source.firstReminderDays,
          secondReminderDays: source.secondReminderDays,
          overdueReminderFrequency: source.overdueReminderFrequency
        });
        // The configured defaults must not overwrite what was copied if they arrive later.
        [priority, firstReminderDays, secondReminderDays, overdueReminderFrequency].forEach((control) => control.markAsDirty());
        this.firstReminderOptions.set(withReminderDay(FIRST_REMINDER_OPTIONS, source.firstReminderDays));
        this.secondReminderOptions.set(withReminderDay(SECOND_REMINDER_OPTIONS, source.secondReminderDays));
        if (source.projectId !== null && projectId.pristine) {
          projectId.setValue(source.projectId);
        }
        this.copySource.set(source);
        this.copyNotice.set({ kind: 'info', message: `Copied from ${source.taskCode}. Check the dates before creating.` });
        this.applyCopyPeople();
      },
      error: () =>
        this.copyNotice.set({ kind: 'warning', message: 'That task could not be copied, so the form is empty.' })
    });
  }

  /** The assignee and recipients can only be applied once both the source task and the user list are in. */
  private applyCopyPeople(): void {
    const source = this.copySource();
    if (!source || this.usersLoading() || this.usersError()) {
      return;
    }

    const assigneeEligible = this.assigneeOptions().some((user) => user.id === source.assignedToUserId);
    if (assigneeEligible && this.form.controls.assignedToUserId.pristine) {
      this.form.controls.assignedToUserId.setValue(source.assignedToUserId);
    } else if (!assigneeEligible) {
      this.copyNotice.set({
        kind: 'warning',
        message: `Copied from ${source.taskCode}, but ${source.assignedToName || 'the original assignee'} can no longer be assigned. Choose another employee.`
      });
    }

    const known = new Set(this.users().map((user) => user.id));
    this.recipientIds.set(source.recipients.map((recipient) => recipient.userId).filter((id) => id !== source.assignedToUserId && known.has(id)));
    this.copySource.set(null);
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

  /** Pre-selects the configured default priority (Screen 20) while the field is still untouched. */
  private loadDefaultPriority(): void {
    this.applicationSettings.get().subscribe({
      next: (settings) => {
        const priority = this.form.controls.priority;
        if (priority.pristine) {
          priority.setValue(settings.defaultPriority);
        }
      },
      error: () => undefined
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
      projectId: value.projectId!,
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
