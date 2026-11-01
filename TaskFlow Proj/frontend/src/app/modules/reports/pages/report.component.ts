import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../shared/auth/auth.service';
import { Permissions } from '../../../shared/auth/permissions';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { ProjectWithCount } from '../../management/project-management/models/project.model';
import { ReportFilter, ReportSummary } from '../models/report.model';
import { TASK_PRIORITIES, TASK_STATUSES, TaskPriority, TaskRecipient, TaskStatus } from '../../monitoring/task-management/models/task.model';
import { ProjectService } from '../../management/project-management/services/project.service';
import { ReportService } from '../services/report.service';
import { endOfLocalDay, startOfLocalDay } from '../../../shared/utils/local-day';
import { saveBlob } from '../../../shared/utils/save-blob';
import { ToastService } from '../../../shared/services/toast.service';
import { TaskService } from '../../monitoring/task-management/services/task.service';

/** File name the server sets on the CSV attachment (kept in sync with `ReportCsv.FileName`). */
export const REPORT_FILE_NAME = 'taskflow-report.csv';

/** Task Reports (Screen 17): totals, a completion summary and a CSV export, Manager/Administrator only. */
@Component({
  selector: 'app-report',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './report.component.html',
  styleUrl: './report.component.css'
})
export class ReportComponent implements OnInit {
  private readonly authService = inject(AuthService);

  readonly canExport = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ReportsExport);
  });

  private readonly reportService = inject(ReportService);
  private readonly projectService = inject(ProjectService);
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);

  readonly statuses = TASK_STATUSES;
  readonly priorities = TASK_PRIORITIES;

  readonly summary = signal<ReportSummary | null>(null);
  readonly assignees = signal<TaskRecipient[]>([]);
  readonly projects = signal<ProjectWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly exporting = signal(false);

  readonly deadlineFrom = signal('');
  readonly deadlineTo = signal('');
  readonly assignedToUserId = signal<number | null>(null);
  readonly projectId = signal<number | null>(null);
  readonly status = signal<TaskStatus | ''>('');
  readonly priority = signal<TaskPriority | ''>('');

  readonly completionRate = computed(() => {
    const data = this.summary();
    if (!data || data.totalTasks === 0) {
      return 0;
    }
    return Math.round((data.completedTasks / data.totalTasks) * 100);
  });

  ngOnInit(): void {
    this.projectService.getAll().subscribe({
      next: (projects) => this.projects.set(projects)
    });
    this.taskService.getAssignees().subscribe({
      next: (assignees) => this.assignees.set(assignees)
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reportService
      .getSummary(this.currentFilter())
      .pipe(catchError(() => of(null)))
      .subscribe((summary) => {
        if (summary === null) {
          this.error.set('Could not load the report. Check your connection and try again.');
        } else {
          this.summary.set(summary);
        }
        this.loading.set(false);
      });
  }

  setDeadlineFrom(value: string): void {
    this.deadlineFrom.set(value);
    this.load();
  }

  setDeadlineTo(value: string): void {
    this.deadlineTo.set(value);
    this.load();
  }

  setAssignee(value: string): void {
    this.assignedToUserId.set(value ? Number(value) : null);
    this.load();
  }

  setProject(value: string): void {
    this.projectId.set(value ? Number(value) : null);
    this.load();
  }

  setStatus(value: string): void {
    this.status.set(value as TaskStatus | '');
    this.load();
  }

  setPriority(value: string): void {
    this.priority.set(value as TaskPriority | '');
    this.load();
  }

  resetFilters(): void {
    this.deadlineFrom.set('');
    this.deadlineTo.set('');
    this.assignedToUserId.set(null);
    this.projectId.set(null);
    this.status.set('');
    this.priority.set('');
    this.load();
  }

  exportCsv(): void {
    this.exporting.set(true);
    this.reportService.exportCsv(this.currentFilter()).subscribe({
      next: (blob) => {
        saveBlob(blob, REPORT_FILE_NAME);
        this.exporting.set(false);
      },
      error: () => {
        this.toast.error('Could not export the report. Please try again.');
        this.exporting.set(false);
      }
    });
  }

  /** Date inputs are local calendar days, expanded to the inclusive UTC bounds of the deadline filter. */
  private currentFilter(): ReportFilter {
    const filter: ReportFilter = {};
    if (this.deadlineFrom()) filter.deadlineFrom = startOfLocalDay(this.deadlineFrom());
    if (this.deadlineTo()) filter.deadlineTo = endOfLocalDay(this.deadlineTo());
    if (this.assignedToUserId() !== null) filter.assignedToUserId = this.assignedToUserId();
    if (this.projectId() !== null) filter.projectId = this.projectId();
    if (this.status()) filter.status = this.status() as TaskStatus;
    if (this.priority()) filter.priority = this.priority() as TaskPriority;
    return filter;
  }
}
