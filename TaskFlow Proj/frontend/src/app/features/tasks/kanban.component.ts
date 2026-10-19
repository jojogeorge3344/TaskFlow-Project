import { Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  CdkDropListGroup
} from '@angular/cdk/drag-drop';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { BOARD_STATUSES, TaskCard, TaskRecipient, TaskStatus } from '../../core/models';
import { IconComponent } from '../../core/layout/icon.component';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from './task.service';

interface BoardColumn {
  status: TaskStatus;
  tasks: TaskCard[];
}

const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  InQA: 'In QA',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled'
};

/** Local YYYY-MM-DD key so date-input filters compare in the user's timezone. */
function localDateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

@Component({
  selector: 'app-kanban',
  standalone: true,
  imports: [CdkDropList, CdkDrag, CdkDropListGroup, IconComponent],
  templateUrl: './kanban.component.html',
  styleUrl: './kanban.component.css'
})
export class KanbanComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly columns = signal<BoardColumn[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly statuses = BOARD_STATUSES;
  readonly statusLabels = STATUS_LABELS;

  /** Every task the board may show; filters narrow it into `columns`. */
  private readonly tasks = signal<TaskCard[]>([]);
  private readonly matchedCount = signal(0);

  readonly assignees = signal<TaskRecipient[]>([]);
  readonly assigneeUserId = signal<number | null>(null);
  readonly status = signal<TaskStatus | ''>('');
  readonly deadlineFrom = signal('');
  readonly deadlineTo = signal('');

  /** Only roles that see every task need the assignee filter; others see only their own tasks. */
  readonly canManage = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.TasksViewAll);
  });

  readonly hasFilters = computed(
    () =>
      this.assigneeUserId() !== null ||
      this.status() !== '' ||
      this.deadlineFrom() !== '' ||
      this.deadlineTo() !== ''
  );

  readonly filteredEmpty = computed(
    () => !this.loading() && !this.error() && this.hasFilters() && this.tasks().length > 0 && this.matchedCount() === 0
  );

  ngOnInit(): void {
    if (this.canManage()) {
      this.taskService.getAssignees().subscribe({
        next: (assignees) => this.assignees.set(assignees)
      });
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.taskService.getBoard().subscribe({
      next: (tasks) => {
        this.tasks.set(tasks);
        this.rebuild();
        this.loading.set(false);
      },
      error: () => {
        this.tasks.set([]);
        this.rebuild();
        this.error.set('Could not load the board. Try again.');
        this.loading.set(false);
      }
    });
  }

  label(status: TaskStatus): string {
    return STATUS_LABELS[status];
  }

  taskCount(): number {
    return this.columns().reduce((total, column) => total + column.tasks.length, 0);
  }

  priorityClass(priority: string): string {
    return `kanban__priority--${priority.toLowerCase()}`;
  }

  deadlineLabel(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }

  open(task: TaskCard): void {
    this.router.navigate(['/tasks', task.id]);
  }

  setAssignee(value: string): void {
    this.assigneeUserId.set(value ? Number(value) : null);
    this.rebuild();
  }

  setStatus(value: string): void {
    this.status.set(value as TaskStatus | '');
    this.rebuild();
  }

  setDeadlineFrom(value: string): void {
    this.deadlineFrom.set(value);
    this.rebuild();
  }

  setDeadlineTo(value: string): void {
    this.deadlineTo.set(value);
    this.rebuild();
  }

  clearFilters(): void {
    this.assigneeUserId.set(null);
    this.status.set('');
    this.deadlineFrom.set('');
    this.deadlineTo.set('');
    this.rebuild();
  }

  drop(event: CdkDragDrop<TaskCard[]>, targetStatus: TaskStatus): void {
    if (event.previousContainer === event.container) {
      // Columns are deadline-ordered and rebuilt from the task list, so in-column order is not user-editable.
      return;
    }

    const source = this.columns().find((column) => column.tasks === event.previousContainer.data);
    const target = this.columns().find((column) => column.tasks === event.container.data);
    if (!source || !target || source.status === target.status) {
      return;
    }

    const card = event.item.data as TaskCard;
    if (source.status !== card.status) {
      return;
    }

    this.commitStatusChange(card, source.status, targetStatus);
  }

  private commitStatusChange(card: TaskCard, from: TaskStatus, to: TaskStatus): void {
    this.updateLocalStatus(card, to);

    this.taskService.changeStatus(card.id, { status: to }).subscribe({
      next: () => {
        this.toast.success(`${card.taskCode} moved to ${STATUS_LABELS[to]}`);
      },
      error: (error: unknown) => {
        this.updateLocalStatus(card, from);
        this.toast.error(apiErrorMessage(error, `Could not move ${card.taskCode}.`));
      }
    });
  }

  /** Optimistic status move; confirmed failures move the card back and re-group. */
  private updateLocalStatus(card: TaskCard, status: TaskStatus): void {
    this.tasks.update((tasks) => tasks.map((task) => (task.id === card.id ? { ...task, status } : task)));
    this.rebuild();
  }

  private rebuild(): void {
    const matched = this.tasks().filter((task) => this.matches(task));
    this.matchedCount.set(matched.length);
    this.columns.set(
      BOARD_STATUSES.map((status) => ({
        status,
        tasks: matched.filter((task) => task.status === status)
      }))
    );
  }

  /** Filters combine with AND semantics; deadline range is inclusive on both ends. */
  private matches(task: TaskCard): boolean {
    if (this.assigneeUserId() !== null && task.assignedToUserId !== this.assigneeUserId()) {
      return false;
    }
    if (this.status() !== '' && task.status !== this.status()) {
      return false;
    }
    const day = localDateKey(new Date(task.deadline));
    const from = this.deadlineFrom();
    if (from !== '' && day < from) {
      return false;
    }
    const to = this.deadlineTo();
    if (to !== '' && day > to) {
      return false;
    }
    return true;
  }
}
