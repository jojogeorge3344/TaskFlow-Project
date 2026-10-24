import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { TaskDetails, TaskPriority, TaskStage, TaskStatus } from '../../core/models';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { backTarget } from '../../shared/ui/return-url';
import { ToastService } from '../../shared/ui/toast.service';
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
  styleUrl: './task-details.component.css'
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
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load the task.'));
        this.loading.set(false);
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
    const actualHours = value.actualHours ?? null;
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