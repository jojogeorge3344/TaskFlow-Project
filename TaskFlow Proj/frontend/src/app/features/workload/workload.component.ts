import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { ProjectWithCount, EmployeeWorkload, WorkloadSummary } from '../../core/models';
import { ProjectService } from '../../core/services/project.service';
import { WorkloadService } from '../../core/services/workload.service';

/** Employee Workload (Screen 16): how task load is distributed across employees, Manager/Administrator only. */
@Component({
  selector: 'app-workload',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './workload.component.html',
  styleUrl: './workload.component.css'
})
export class WorkloadComponent implements OnInit {
  private readonly workloadService = inject(WorkloadService);
  private readonly projectService = inject(ProjectService);
  private readonly auth = inject(AuthService);

  /** Filters and the link to an employee's task list are separate permissions from opening the screen. */
  readonly canFilter = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.WorkloadFilters);
  });
  readonly canOpenTasks = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.WorkloadTasksOpen);
  });

  readonly rows = signal<EmployeeWorkload[]>([]);
  readonly projects = signal<ProjectWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly projectId = signal<number | null>(null);
  readonly employeeId = signal<number | null>(null);

  /** Rows narrowed by the in-page employee filter; the summary and bars follow the same subset. */
  readonly filteredRows = computed<EmployeeWorkload[]>(() => {
    const employeeId = this.employeeId();
    return employeeId === null ? this.rows() : this.rows().filter((row) => row.userId === employeeId);
  });

  readonly summary = computed<WorkloadSummary>(() => {
    const rows = this.filteredRows();
    const headcount = rows.length;
    const totalActive = rows.reduce((sum, row) => sum + row.activeTasks, 0);
    const averageActive = headcount === 0 ? 0 : totalActive / headcount;
    return { headcount, totalActive, averageActive, highLoadThreshold: averageActive * 2 };
  });

  /** The busiest active count, used to scale the CSS bars (never zero to avoid dividing by zero). */
  readonly maxActive = computed(() => Math.max(1, ...this.filteredRows().map((row) => row.activeTasks)));

  ngOnInit(): void {
    this.projectService.getAll().subscribe({
      next: (projects) => this.projects.set(projects)
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.workloadService
      .getWorkload(this.projectId())
      .pipe(catchError(() => of(null)))
      .subscribe((rows) => {
        if (rows === null) {
          this.error.set('Could not load the workload. Check your connection and try again.');
        } else {
          this.rows.set(rows);
        }
        this.loading.set(false);
      });
  }

  setProject(value: string): void {
    this.projectId.set(value ? Number(value) : null);
    this.employeeId.set(null);
    this.load();
  }

  setEmployee(value: string): void {
    this.employeeId.set(value ? Number(value) : null);
  }

  reset(): void {
    this.projectId.set(null);
    this.employeeId.set(null);
    this.load();
  }

  /** "Unusually high" is a client-side rule: at or above twice the average active count. */
  isHighLoad(row: EmployeeWorkload): boolean {
    const threshold = this.summary().highLoadThreshold;
    return threshold > 0 && row.activeTasks >= threshold;
  }

  barWidth(row: EmployeeWorkload): number {
    return Math.round((row.activeTasks / this.maxActive()) * 100);
  }

  averageLabel(): string {
    return this.summary().averageActive.toFixed(1);
  }
}
