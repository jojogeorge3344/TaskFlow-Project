import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, filter, map, of, switchMap } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { DrillDownFilter, parseDrillDown } from '../../../../shared/utils/drill-down';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ProjectService } from '../../../management/project-management/services/project.service';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { endOfLocalDay, startOfLocalDay } from '../../../../shared/utils/local-day';
import { PagedResult, TASK_PRIORITIES, TASK_STATUSES, TaskListFilter, TaskListItem, TaskPriority, TaskRecipient, TaskStage, TaskStatus } from '../models/task.model';
import { ProjectWithCount } from '../../../management/project-management/models/project.model';
import { TaskService } from '../services/task.service';

export { endOfLocalDay, startOfLocalDay } from '../../../../shared/utils/local-day';

export const SEARCH_DEBOUNCE_MS = 300;
export const PAGE_SIZE = 20;

export const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  InQA: 'In QA',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled'
};

const STAGE_LABELS: Partial<Record<TaskStage, string>> = {
  Overdue: 'Overdue',
  DueToday: 'Due today',
  Upcoming: 'Upcoming'
};

/** Task List (Screen 7): every task the caller may access, searchable, filterable and paged. */
@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.css'
})
export class TaskListComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly projectService = inject(ProjectService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  private readonly searchInput$ = new Subject<string>();
  private readonly reload$ = new Subject<void>();

  readonly statuses = TASK_STATUSES;
  readonly priorities = TASK_PRIORITIES;
  readonly statusLabels = STATUS_LABELS;

  /** Roles that see every task get the employee filter and the "Task Management" title. */
  readonly canManage = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.TasksViewAll);
  });

  readonly canCreate = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.TasksCreate);
  });

  /** Deleting a task needs its own permission. */
  readonly canDelete = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.TasksDelete);
  });

  readonly deletingId = signal<number | null>(null);

  readonly tasks = signal<TaskListItem[]>([]);
  readonly total = signal(0);
  readonly assignees = signal<TaskRecipient[]>([]);
  /** Projects for the filter; stays empty (and the filter hidden) if they cannot be loaded. */
  readonly projects = signal<ProjectWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  /** What is typed in the search box; `search` is the debounced value that is applied. */
  readonly searchText = signal('');
  readonly search = signal('');
  readonly assignedToUserId = signal<number | null>(null);
  readonly projectId = signal<number | null>(null);
  readonly status = signal<TaskStatus | ''>('');
  readonly priority = signal<TaskPriority | ''>('');
  readonly deadlineFrom = signal('');
  readonly deadlineTo = signal('');
  readonly page = signal(1);
  readonly drill = signal<DrillDownFilter | null>(null);

  readonly hasFilters = computed(
    () =>
      this.search() !== '' ||
      this.assignedToUserId() !== null ||
      this.projectId() !== null ||
      this.status() !== '' ||
      this.priority() !== '' ||
      this.deadlineFrom() !== '' ||
      this.deadlineTo() !== '' ||
      this.drill() !== null
  );

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  readonly rangeLabel = computed(() => {
    const total = this.total();
    if (total === 0) {
      return '0 tasks';
    }
    const first = (this.page() - 1) * PAGE_SIZE + 1;
    const last = Math.min(this.page() * PAGE_SIZE, total);
    return `${first}–${last} of ${total} ${total === 1 ? 'task' : 'tasks'}`;
  });

  constructor() {
    // The workload screen links here with `?assignedToUserId=` to open one employee's task list.
    const assignedToUserId = Number(this.route.snapshot.queryParamMap.get('assignedToUserId'));
    if (Number.isInteger(assignedToUserId) && assignedToUserId > 0) {
      this.assignedToUserId.set(assignedToUserId);
    }

    const drill = parseDrillDown(this.route.snapshot.queryParamMap);
    if (drill) {
      const { priority, ...request } = drill.request;
      if (priority) {
        this.priority.set(priority);
      }
      this.drill.set({ request, summary: drill.summary });
    }

    this.searchInput$
      .pipe(
        debounceTime(SEARCH_DEBOUNCE_MS),
        map((term) => term.trim()),
        // Skip when the applied search is unchanged (e.g. only whitespace was added).
        filter((term) => term !== this.search()),
        takeUntilDestroyed()
      )
      .subscribe((term) => {
        this.search.set(term);
        this.reloadFromFirstPage();
      });

    // switchMap cancels the previous request so only the latest filters win.
    this.reload$
      .pipe(
        switchMap(() => this.fetchTasks()),
        takeUntilDestroyed()
      )
      .subscribe((result) => {
        if (result === null) {
          this.error.set('Could not load tasks. Check your connection and try again.');
        } else {
          this.tasks.set(result.items);
          this.total.set(result.total);
        }
        this.loading.set(false);
      });
  }

  ngOnInit(): void {
    if (this.canManage()) {
      this.taskService.getAssignees().subscribe({
        next: (assignees) => this.assignees.set(assignees)
      });
    }
    this.projectService.getAll().subscribe({
      next: (projects) => this.projects.set(projects),
      error: () => this.projects.set([])
    });
    this.load();
  }

  load(): void {
    this.reload$.next();
  }

  async deleteTask(task: TaskListItem): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Delete task?',
      message: `${task.taskCode} "${task.title}" will be removed for everyone. Its comments and history are kept.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.deletingId.set(task.id);
    this.taskService.delete(task.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.toast.success(`${task.taskCode} deleted`);
        // The last row of a later page is gone, so step back instead of showing an empty page.
        if (this.tasks().length === 1 && this.page() > 1) {
          this.page.set(this.page() - 1);
        }
        this.load();
      },
      error: (err: unknown) => {
        this.deletingId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not delete the task.'));
      }
    });
  }

  onSearch(value: string): void {
    this.searchText.set(value);
    this.searchInput$.next(value);
  }

  setAssignee(value: string): void {
    this.assignedToUserId.set(value ? Number(value) : null);
    this.reloadFromFirstPage();
  }

  setProject(value: string): void {
    this.projectId.set(value ? Number(value) : null);
    this.reloadFromFirstPage();
  }

  setStatus(value: string): void {
    this.status.set(value as TaskStatus | '');
    this.reloadFromFirstPage();
  }

  setPriority(value: string): void {
    this.priority.set(value as TaskPriority | '');
    this.reloadFromFirstPage();
  }

  setDeadlineFrom(value: string): void {
    this.deadlineFrom.set(value);
    this.reloadFromFirstPage();
  }

  setDeadlineTo(value: string): void {
    this.deadlineTo.set(value);
    this.reloadFromFirstPage();
  }

  resetFilters(): void {
    this.searchText.set('');
    this.search.set('');
    this.assignedToUserId.set(null);
    this.projectId.set(null);
    this.status.set('');
    this.priority.set('');
    this.deadlineFrom.set('');
    this.deadlineTo.set('');
    this.drill.set(null);
    this.reloadFromFirstPage();
  }

  clearDrill(): void {
    this.drill.set(null);
    this.reloadFromFirstPage();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.pageCount() || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.load();
  }

  stageLabel(task: TaskListItem): string | null {
    return STAGE_LABELS[task.stage] ?? null;
  }

  priorityClass(priority: TaskPriority): string {
    return `priority priority--${priority.toLowerCase()}`;
  }

  date(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }

  private reloadFromFirstPage(): void {
    this.page.set(1);
    this.load();
  }

  private fetchTasks(): Observable<PagedResult<TaskListItem> | null> {
    this.loading.set(true);
    this.error.set(null);

    const request: TaskListFilter = { page: this.page(), pageSize: PAGE_SIZE };
    if (this.search()) request.search = this.search();
    if (this.assignedToUserId() !== null) request.assignedToUserId = this.assignedToUserId();
    if (this.projectId() !== null) request.projectId = this.projectId();
    if (this.status()) request.status = this.status() as TaskStatus;
    if (this.priority()) request.priority = this.priority() as TaskPriority;
    // Date inputs are local calendar days, matching how deadlines are shown in the table.
    if (this.deadlineFrom()) request.deadlineFrom = startOfLocalDay(this.deadlineFrom());
    if (this.deadlineTo()) request.deadlineTo = endOfLocalDay(this.deadlineTo());

    Object.assign(request, this.drill()?.request);

    Object.assign(request, this.drill()?.request);

    return this.taskService.list(request).pipe(catchError(() => of(null)));
  }
}
