import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { Project, ProjectMember, ProjectTask, ProjectTaskFilter } from '../models/project.model';
import { TASK_PRIORITIES, TASK_STATUSES, TaskPriority, TaskStatus } from '../../../monitoring/task-management/models/task.model';
import { ProjectService } from '../services/project.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { Filters } from './project-detail.model';

const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  InQA: 'In QA',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled'
};

const NO_FILTERS: Filters = { status: '', priority: '', userId: null, positionId: null, from: '', to: '' };

/** Project detail: who is on the project in which role, and the tasks mapped to it with filters. */
@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.css'
})
export class ProjectDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectService);

  readonly statuses = TASK_STATUSES;
  readonly priorities = TASK_PRIORITIES;
  readonly statusLabels = STATUS_LABELS;

  readonly project = signal<Project | null>(null);
  readonly members = signal<ProjectMember[]>([]);
  /** Every task of the project, unfiltered: the summary numbers come from here. */
  readonly allTasks = signal<ProjectTask[]>([]);
  /** The tasks matching the current filters. */
  readonly tasks = signal<ProjectTask[]>([]);
  readonly filters = signal<Filters>({ ...NO_FILTERS });
  readonly loading = signal(true);
  readonly tasksLoading = signal(false);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly tasksError = signal<string | null>(null);
  protected projectId = 0;
  private requestId = 0;

  /** Only the roles held on this project, each once. */
  readonly roleOptions = computed(() => {
    const seen = new Map<number, string>();
    for (const member of this.members()) {
      seen.set(member.positionId, member.positionName);
    }
    return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  readonly filtersActive = computed(() => {
    const f = this.filters();
    return f.status !== '' || f.priority !== '' || f.userId !== null || f.positionId !== null || f.from !== '' || f.to !== '';
  });

  readonly stats = computed(() => {
    const tasks = this.allTasks();
    const done = tasks.filter((task) => task.status === 'Completed').length;
    return {
      members: this.members().length,
      tasks: tasks.length,
      overdue: tasks.filter((task) => this.isOverdue(task)).length,
      completed: tasks.length === 0 ? 0 : Math.round((done / tasks.length) * 100)
    };
  });

  ngOnInit(): void {
    this.projectId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.notFound.set(false);
    this.projectService.getById(this.projectId).subscribe({
      next: (project) => {
        this.project.set(project);
        this.projectService.getMembers(this.projectId).subscribe({
          next: (members) => this.members.set(members),
          error: () => this.members.set([])
        });
        this.projectService.getTasks(this.projectId).subscribe({
          next: (tasks) => {
            this.allTasks.set(tasks);
            this.tasks.set(tasks);
            this.loading.set(false);
          },
          error: (err) => {
            this.error.set(apiErrorMessage(err, 'Could not load the project tasks.'));
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        if (err?.status === 404) {
          this.notFound.set(true);
        } else {
          this.error.set(apiErrorMessage(err, 'Could not load the project.'));
        }
        this.loading.set(false);
      }
    });
  }

  setFilter<K extends keyof Filters>(key: K, value: Filters[K]): void {
    this.filters.update((current) => ({ ...current, [key]: value }));
    this.applyFilters();
  }

  setNumberFilter(key: 'userId' | 'positionId', raw: string): void {
    this.setFilter(key, raw === '' ? null : Number(raw));
  }

  clearFilters(): void {
    this.filters.set({ ...NO_FILTERS });
    this.tasks.set(this.allTasks());
    this.tasksError.set(null);
  }

  private applyFilters(): void {
    if (!this.filtersActive()) {
      this.clearFilters();
      return;
    }
    const f = this.filters();
    const request: ProjectTaskFilter = {
      status: f.status || null,
      priority: f.priority || null,
      assignedToUserId: f.userId,
      positionId: f.positionId,
      deadlineFrom: f.from ? new Date(`${f.from}T00:00:00`).toISOString() : null,
      deadlineTo: f.to ? new Date(`${f.to}T23:59:59.999`).toISOString() : null
    };
    const id = ++this.requestId;
    this.tasksLoading.set(true);
    this.tasksError.set(null);
    this.projectService.getTasks(this.projectId, request).subscribe({
      next: (tasks) => {
        if (id === this.requestId) {
          this.tasks.set(tasks);
          this.tasksLoading.set(false);
        }
      },
      error: (err) => {
        if (id === this.requestId) {
          this.tasksError.set(apiErrorMessage(err, 'Could not load the tasks.'));
          this.tasksLoading.set(false);
        }
      }
    });
  }

  isOverdue(task: ProjectTask): boolean {
    return task.status !== 'Completed' && task.status !== 'Cancelled' && new Date(task.deadline).getTime() < Date.now();
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join('');
  }

  date(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }
}
