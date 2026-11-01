import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, map, of, switchMap, tap } from 'rxjs';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { AssigneeCandidate, CreateTaskRequest, ReminderFrequency, TASK_PRIORITIES, TaskDetails, TaskPriority } from '../models/task.model';
import { ProjectWithCount } from '../../../management/project-management/models/project.model';
import { User } from '../../../management/employee-management/models/user.model';
import { ApplicationSettingsService } from '../../../settings/application-settings/services/application-settings.service';
import { ProjectService } from '../../../management/project-management/services/project.service';
import { ReminderSettingsService } from '../../../settings/reminder-settings/services/reminder-settings.service';
import { UserService } from '../../../management/employee-management/services/user.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  FIRST_REMINDER_OPTIONS,
  OVERDUE_FREQUENCY_OPTIONS,
  ReminderDayOption,
  SECOND_REMINDER_OPTIONS,
  withReminderDay
} from '../../../../shared/utils/reminder-options';
import { TaskService } from '../services/task.service';
import { FieldName } from './task-create.model';

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

/** Where the assignee dropdown's data stands: no project chosen, fetching the project's team, have it, or failed. */
export type CandidatesState = 'idle' | 'loading' | 'loaded' | 'error';

/** The dropdown text for a team member: name, code, and their current load (overdue only when there is any). */
export function candidateLabel(candidate: AssigneeCandidate): string {
  const code = candidate.employeeCode ? ` (${candidate.employeeCode})` : '';
  const overdue = candidate.overdueTasks > 0 ? ` - ${candidate.overdueTasks} overdue` : '';
  return `${candidate.fullName}${code} - ${candidate.activeTasks} active${overdue}`;
}

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
  /** The chosen project's team with each member's current load, least-loaded first (the server's pick order). */
  readonly candidates = signal<AssigneeCandidate[]>([]);
  readonly candidatesState = signal<CandidatesState>('idle');
  readonly candidateLabel = candidateLabel;
  /** The team member picked automatically for the current project, so the hint can say so until someone else is chosen. */
  private readonly autoPickedId = signal<number | null>(null);
  /** True while a `?copyFrom=` task is loading, so a team that arrives first is not auto-picked over the copied assignee. */
  private copyPending = false;
  /** The copied task's assignee, waiting for the copied project's team to arrive. */
  private copiedAssignee: { userId: number; name: string; taskCode: string } | null = null;

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

  /** Anyone active except the assignee, who is always a recipient anyway. */
  readonly recipientOptions = computed(() => this.users().filter((user) => user.id !== this.assigneeId()));

  /** The line under the assignee field: why the employee was picked (until the user chooses someone else), or why
   * nobody could be. */
  readonly assigneeNotice = computed<{ kind: 'info' | 'warning'; message: string } | null>(() => {
    switch (this.candidatesState()) {
      case 'error':
        return { kind: 'warning', message: "This project's team could not be loaded." };
      case 'loaded': {
        if (this.candidates().length === 0) {
          return { kind: 'warning', message: 'No employees are mapped to this project.' };
        }
        const picked = this.candidates().find((candidate) => candidate.userId === this.autoPickedId());
        return picked && picked.userId === this.assigneeId()
          ? { kind: 'info', message: `Auto-selected: fewest active tasks on this project's team (${picked.activeTasks} active).` }
          : null;
      }
      default:
        return null;
    }
  });

  constructor() {
    // Every project change fetches that project's team afresh; switchMap drops a reply for a project no longer chosen.
    this.form.controls.projectId.valueChanges
      .pipe(
        tap((projectId) => {
          this.candidates.set([]);
          this.autoPickedId.set(null);
          this.candidatesState.set(projectId === null ? 'idle' : 'loading');
          this.form.controls.assignedToUserId.setValue(null);
        }),
        switchMap((projectId) =>
          projectId === null
            ? EMPTY
            : this.taskService.getAssigneeCandidates(projectId).pipe(
                map((candidates) => ({ candidates, failed: false })),
                catchError(() => of({ candidates: [] as AssigneeCandidate[], failed: true }))
              )
        ),
        takeUntilDestroyed()
      )
      .subscribe(({ candidates, failed }) => {
        this.candidates.set(candidates);
        this.candidatesState.set(failed ? 'error' : 'loaded');
        if (!this.copyPending) {
          this.applyAssignee();
        }
      });
  }

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
        this.applyCopyRecipients();
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

    this.copyPending = true;
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
        this.copySource.set(source);
        this.copyNotice.set({ kind: 'info', message: `Copied from ${source.taskCode}. Check the dates before creating.` });
        this.copiedAssignee = { userId: source.assignedToUserId, name: source.assignedToName, taskCode: source.taskCode };
        this.copyPending = false;
        if (source.projectId !== null && projectId.pristine) {
          projectId.setValue(source.projectId);
        } else if (this.candidatesState() === 'loaded') {
          // The project was already chosen and its team is in, so apply the copied assignee to it now.
          this.applyAssignee();
        }
        this.applyCopyRecipients();
      },
      error: () => {
        this.copyPending = false;
        this.copyNotice.set({ kind: 'warning', message: 'That task could not be copied, so the form is empty.' });
        if (this.candidatesState() === 'loaded') {
          this.applyAssignee();
        }
      }
    });
  }

  /** The copied recipients can only be applied once both the source task and the user list are in. */
  private applyCopyRecipients(): void {
    const source = this.copySource();
    if (!source || this.usersLoading() || this.usersError()) {
      return;
    }

    const known = new Set(this.users().map((user) => user.id));
    this.recipientIds.set(source.recipients.map((recipient) => recipient.userId).filter((id) => id !== source.assignedToUserId && known.has(id)));
    this.copySource.set(null);
  }

  /** Once the chosen project's team is in: keeps a copied task's assignee if they are on it, otherwise selects the first
   * (least-loaded) team member, or nobody when the team is empty. */
  private applyAssignee(): void {
    const candidates = this.candidates();
    const copied = this.copiedAssignee;
    this.copiedAssignee = null;

    if (copied && candidates.some((candidate) => candidate.userId === copied.userId)) {
      this.form.controls.assignedToUserId.setValue(copied.userId);
      return;
    }

    const pick = candidates[0] ?? null;
    this.autoPickedId.set(pick?.userId ?? null);
    this.form.controls.assignedToUserId.setValue(pick?.userId ?? null);
    if (copied) {
      const who = copied.name || 'the original assignee';
      this.copyNotice.set({
        kind: 'warning',
        message: pick
          ? `Copied from ${copied.taskCode}, but ${who} is no longer on this project's team, so the least-loaded team member was selected.`
          : `Copied from ${copied.taskCode}, but ${who} can no longer be assigned. Choose another employee.`
      });
    }
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
