import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { ActivityEntry, TaskComment, TaskDetails, TaskPriority, TaskStage, TaskStatus } from '../../core/models';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { initials } from '../../shared/ui/initials';
import { backTarget } from '../../shared/ui/return-url';
import { ToastService } from '../../shared/ui/toast.service';
import { ActivityEventMeta, eventMeta } from './activity-meta';
import { TaskService } from './task.service';

const STAGE_LABELS: Record<TaskStage, string> = {
  Upcoming: 'Upcoming',
  DueToday: 'Due today',
  Overdue: 'Overdue',
  Completed: 'Completed'
};

const PRIORITY_LABELS: Record<TaskPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical'
};

function deadlineAfterStartValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as string;
  const deadline = group.get('deadline')?.value as string;
  if (start && deadline && new Date(deadline) <= new Date(start)) {
    return { deadlineBeforeStart: true };
  }
  return null;
}

function toInputDate(iso: string): string {
  return new Date(iso).toISOString().slice(0, 16);
}

@Component({
  selector: 'app-task-details',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './task-details.component.html',
  styleUrls: ['./task-details.component.css', './task-details-tabs.component.css']
})
export class TaskDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  /** Where Back goes: the screen the user came from (validated `returnUrl`), else the task list. */
  readonly back = backTarget(this.route.snapshot.queryParamMap.get('returnUrl'));

  readonly task = signal<TaskDetails | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly changingStatus = signal<TaskStatus | null>(null);
  readonly deleting = signal(false);
  readonly comments = signal<TaskComment[]>([]);
  readonly commentsLoading = signal(true);
  readonly loggingHours = signal(false);
  readonly hoursToLog = signal('');

  readonly editForm = this.fb.group(
    {
      title: this.fb.control('', [Validators.required, Validators.maxLength(200)]),
      description: this.fb.control(''),
      priority: this.fb.control<TaskPriority>('Medium'),
      startDate: this.fb.control('', Validators.required),
      deadline: this.fb.control('', Validators.required),
      estimatedHours: this.fb.control<number>(0, [Validators.required, Validators.min(0.5)]),
      actualHours: this.fb.control<number | null>(null)
    },
    { validators: [deadlineAfterStartValidator] }
  );

  readonly canEdit = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksUpdate);
  });

  /** Whoever may edit tasks, or the employee the task is assigned to, can log worked hours on it. */
  readonly canLogHours = computed(() => {
    const user = this.authService.currentUser();
    const task = this.task();
    return this.canEdit() || (!!user && !!task && task.assignedToUserId === user.id);
  });

  readonly canViewActivity = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksActivityView);
  });

  readonly canCreate = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksCreate);
  });

  readonly canReassign = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksReassign);
  });

  /** Deleting a task needs its own permission. */
  readonly canDelete = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksDelete);
  });

  /** Remaining = estimated - worked; a task with no worked hours yet has all its estimate left. */
  readonly remainingHours = computed(() => {
    const task = this.task();
    return task ? task.estimatedHours - (task.actualHours ?? 0) : null;
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.load(id);
  }

  load(id = this.task()?.id ?? 0): void {
    if (!id) {
      return;
    }
    const taskId = id;
    this.loading.set(true);
    this.error.set(null);

    this.taskService.getById(taskId).subscribe({
      next: (details) => {
        this.task.set(details);
        this.loading.set(false);
        this.loadComments(taskId);
        this.loadActivity(taskId);
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load the task.'));
        this.loading.set(false);
      }
    });
  }

  readonly activeTab = signal<'discussion' | 'activity' | 'reassign'>('discussion');
  readonly activity = signal<ActivityEntry[]>([]);
  readonly activityLoading = signal(false);

  /** The three newest comments (the API lists them oldest first). */
  readonly latestComments = computed(() => this.comments().slice(-3));

  /** The four newest activity entries, newest first. */
  readonly latestActivity = computed(() => this.activity().slice(0, 4));

  setTab(tab: 'discussion' | 'activity' | 'reassign'): void {
    this.activeTab.set(tab);
  }

  activityMeta(type: string): ActivityEventMeta {
    return eventMeta(type);
  }

  private loadActivity(taskId: number): void {
    if (!this.canViewActivity()) {
      this.activity.set([]);
      return;
    }
    this.activityLoading.set(true);
    this.taskService.getActivity(taskId).subscribe({
      next: (entries) => {
        this.activity.set(
          [...entries].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() || b.id - a.id)
        );
        this.activityLoading.set(false);
      },
      error: () => {
        this.activity.set([]);
        this.activityLoading.set(false);
      }
    });
  }

  private loadComments(taskId: number): void {
    this.commentsLoading.set(true);
    this.taskService.getComments(taskId).subscribe({
      next: (comments) => {
        this.comments.set(comments);
        this.commentsLoading.set(false);
      },
      error: () => {
        this.comments.set([]);
        this.commentsLoading.set(false);
      }
    });
  }

  initialsOf(name: string | null | undefined): string {
    return initials(name);
  }

  timeLabel(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  /** Adds the entered hours to the task's worked total; the backend records a HoursLogged activity entry. */
  logHours(): void {
    const task = this.task();
    const hours = Number(this.hoursToLog());
    if (!task || this.loggingHours()) {
      return;
    }
    if (!Number.isFinite(hours) || hours <= 0) {
      this.toast.error('Enter the hours worked (greater than 0).');
      return;
    }

    this.loggingHours.set(true);
    this.taskService
      .logHours(task.id, hours)
      .subscribe({
        next: (updated) => {
          this.loggingHours.set(false);
          this.hoursToLog.set('');
          this.task.set(updated);
          if (this.editing()) {
            this.editForm.patchValue({ actualHours: updated.actualHours ?? null });
          }
          this.toast.success(`${hours}h logged`);
        },
        error: (err: unknown) => {
          this.loggingHours.set(false);
          this.toast.error(apiErrorMessage(err, 'Could not log the hours.'));
        }
      });
  }

  async deleteTask(): Promise<void> {
    const task = this.task();
    if (!task || this.deleting()) {
      return;
    }

    const confirmed = await this.confirmDialog().open({
      title: 'Delete task?',
      message: `${task.taskCode} "${task.title}" will be removed for everyone. Its comments and history are kept.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.deleting.set(true);
    this.taskService.delete(task.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.toast.success(`${task.taskCode} deleted`);
        void this.router.navigate(['/tasks']);
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not delete the task.'));
      }
    });
  }

  stageLabel(stage: TaskStage): string {
    return STAGE_LABELS[stage];
  }

  stageClass(stage: TaskStage): string {
    return `tf-chip tf-chip--${stage.toLowerCase()}`;
  }

  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

  descriptionLength(): number {
    return (this.editForm.controls.description.value ?? '').length;
  }

  setPriority(priority: TaskPriority): void {
    this.editForm.controls.priority.setValue(priority);
    this.editForm.controls.priority.markAsDirty();
  }

  /** The stepper buttons move a number field by `delta`, never below 0. */
  stepHours(field: 'estimatedHours' | 'actualHours', delta: number): void {
    const control = this.editForm.controls[field];
    const next = Math.max(0, Math.round(((control.value ?? 0) + delta) * 100) / 100);
    control.setValue(next);
    control.markAsDirty();
    control.markAsTouched();
  }

  /** Worked against estimated hours, as typed in the form right now. */
  effort(): { worked: number; estimated: number; percent: number; over: boolean; label: string } {
    const worked = Number(this.editForm.controls.actualHours.value ?? 0) || 0;
    const estimated = Number(this.editForm.controls.estimatedHours.value ?? 0) || 0;
    const percent = estimated > 0 ? Math.min(100, Math.round((worked / estimated) * 100)) : 0;
    const remaining = Math.round((estimated - worked) * 100) / 100;
    const over = estimated > 0 && worked > estimated;
    const label = over ? `${Math.abs(remaining)}h over estimate` : `${remaining}h remaining · ${percent}%`;
    return { worked, estimated, percent, over, label };
  }

  /** How long the task runs and how far through it today is, from the dates typed in the form right now. */
  scheduleSummary(): { length: string; due: string; late: boolean; percent: number } | null {
    const start = new Date(this.editForm.controls.startDate.value ?? '').getTime();
    const end = new Date(this.editForm.controls.deadline.value ?? '').getTime();
    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) {
      return null;
    }

    const hourMs = 3_600_000;
    const totalHours = Math.round((end - start) / hourMs);
    const days = Math.floor(totalHours / 24);
    const hours = totalHours % 24;
    const length = [days ? `${days} day${days === 1 ? '' : 's'}` : '', hours ? `${hours} hour${hours === 1 ? '' : 's'}` : '']
      .filter(Boolean)
      .join(', ') + ' long';

    const now = Date.now();
    const leftHours = Math.round((end - now) / hourMs);
    const late = leftHours < 0;
    const span = Math.abs(leftHours);
    const spanText = span >= 48 ? `${Math.round(span / 24)} days` : span >= 1 ? `${span} hour${span === 1 ? '' : 's'}` : 'less than an hour';
    const due = late ? `Overdue by ${spanText}` : `Due in ${spanText}`;
    const percent = Math.min(100, Math.max(0, Math.round(((now - start) / (end - start)) * 100)));
    return { length, due, late, percent };
  }

  priorityLabel(priority: string): string {
    return PRIORITY_LABELS[priority as TaskPriority] ?? priority;
  }

  dateLabel(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }

  allowedNext(status: TaskStatus): TaskStatus[] {
    switch (status) {
      case 'ToDo':
        return ['InProgress', 'Blocked', 'Cancelled'];
      case 'InProgress':
        return ['InQA', 'Blocked', 'Completed', 'Cancelled'];
      case 'InQA':
        return ['InProgress', 'Blocked', 'Completed', 'Cancelled'];
      case 'Blocked':
        return ['InProgress', 'Cancelled'];
      default:
        return [];
    }
  }

  changeStatus(status: TaskStatus): void {
    const task = this.task();
    if (!task) {
      return;
    }

    this.changingStatus.set(status);
    this.taskService.changeStatus(task.id, { status }).subscribe({
      next: () => {
        this.changingStatus.set(null);
        this.toast.success(`${task.taskCode} moved to ${status}`);
        this.load(task.id);
      },
      error: (err: unknown) => {
        this.changingStatus.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not update the status.'));
      }
    });
  }

  startEdit(): void {
    const task = this.task();
    if (!task) {
      return;
    }

    this.editForm.patchValue({
      title: task.title,
      description: task.description ?? '',
      priority: task.priority,
      startDate: toInputDate(task.startDate),
      deadline: toInputDate(task.deadline),
      estimatedHours: task.estimatedHours,
      actualHours: task.actualHours ?? null
    });
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
  }

  saveEdit(): void {
    const task = this.task();
    if (!task || this.editForm.invalid) {
      return;
    }

    const value = this.editForm.getRawValue();
    const start = new Date(value.startDate ?? new Date());
    const deadline = new Date(value.deadline ?? new Date());

    this.saving.set(true);
    // A cleared field means "no worked hours": send 0, since the backend ignores null.
    const actualHours = value.actualHours ?? 0;
    this.taskService
      .update(task.id, {
        title: value.title?.trim() ?? '',
        description: value.description?.trim() || null,
        priority: value.priority ?? 'Medium',
        startDate: start.toISOString(),
        deadline: deadline.toISOString(),
        estimatedHours: value.estimatedHours ?? 0,
        actualHours: actualHours
      })
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.editing.set(false);
          this.task.set(updated);
          this.toast.success('Task updated');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.toast.error(apiErrorMessage(err, 'Could not save the task.'));
        }
      });
  }

  goBack(): void {
    void this.router.navigateByUrl(this.back.url);
  }
}