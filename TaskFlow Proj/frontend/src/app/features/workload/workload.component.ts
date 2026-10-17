import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { IconComponent } from '../../core/layout/icon.component';
import { DepartmentWithCount, EmployeeWorkload, WorkloadSummary } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
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
  private readonly departmentService = inject(DepartmentService);

  readonly rows = signal<EmployeeWorkload[]>([]);
  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly departmentId = signal<number | null>(null);

  readonly summary = computed<WorkloadSummary>(() => {
    const rows = this.rows();
    const headcount = rows.length;
    const totalActive = rows.reduce((sum, row) => sum + row.activeTasks, 0);
    const averageActive = headcount === 0 ? 0 : totalActive / headcount;
    return { headcount, totalActive, averageActive, highLoadThreshold: averageActive * 2 };
  });

  /** The busiest active count, used to scale the CSS bars (never zero to avoid dividing by zero). */
  readonly maxActive = computed(() => Math.max(1, ...this.rows().map((row) => row.activeTasks)));

  ngOnInit(): void {
    this.departmentService.getAll().subscribe({
      next: (departments) => this.departments.set(departments)
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.workloadService
      .getWorkload(this.departmentId())
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

  setDepartment(value: string): void {
    this.departmentId.set(value ? Number(value) : null);
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
