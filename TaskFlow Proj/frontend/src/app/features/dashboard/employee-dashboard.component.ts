import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { shortDate } from '../../core/analytics/analytics-filter';
import { DrillTarget, employeeDrillQuery } from '../../core/analytics/drill-down';
import { AuthService } from '../../core/auth/auth.service';
import { EMPLOYEE_PERIODS, EmployeeDeadline, EmployeePeriod, TaskBucket, TaskPriority } from '../../core/models';
import { EmployeeAnalyticsService } from '../../core/services/employee-analytics.service';
import { BUCKET_SERIES, PRIORITY_COLORS } from '../../shared/charts/chart-colors';
import { provideDashboardCharts } from '../../shared/charts/chart-providers';
import { DonutChartComponent } from '../../shared/charts/donut-chart.component';
import { DonutSlice } from '../../shared/charts/donut-chart.model';
import { KpiCardComponent } from '../../shared/charts/kpi-card.component';
import { LineChartComponent } from '../../shared/charts/line-chart.component';
import { LineSeries, PointClick } from '../../shared/charts/line-chart.model';
import { ProgressListComponent } from '../../shared/charts/progress-list.component';
import { ProgressItem } from '../../shared/charts/progress-list.model';

const PRIORITY_ORDER: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

@Component({
  selector: 'app-employee-dashboard',
  standalone: true,
  imports: [RouterLink, DatePipe, KpiCardComponent, DonutChartComponent, LineChartComponent, ProgressListComponent],
  providers: [provideDashboardCharts()],
  templateUrl: './employee-dashboard.component.html',
  styleUrl: './employee-dashboard.component.css'
})
export class EmployeeDashboardComponent implements OnInit {
  private readonly analytics = inject(EmployeeAnalyticsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;
  readonly periods = EMPLOYEE_PERIODS;

  readonly period = this.analytics.period;
  readonly data = this.analytics.data;
  readonly loading = this.analytics.loading;
  readonly error = this.analytics.error;

  readonly periodLabel = computed(() => EMPLOYEE_PERIODS.find((item) => item.key === this.period())?.label ?? '');

  readonly hasNoTasks = computed(() => !this.loading() && !this.error() && this.data()?.summary.totalTasks === 0);

  readonly statusSlices = computed<DonutSlice[]>(() => {
    const counts = this.data()?.tasksByStatus;
    return BUCKET_SERIES.map((series) => ({
      key: series.key,
      label: series.label,
      color: series.color,
      value: counts ? counts[bucketField(series.key as TaskBucket)] : 0
    }));
  });

  readonly prioritySlices = computed<DonutSlice[]>(() =>
    [...(this.data()?.tasksByPriority ?? [])]
      .sort(
        (first, second) =>
          PRIORITY_ORDER.indexOf(second.priority as TaskPriority) - PRIORITY_ORDER.indexOf(first.priority as TaskPriority)
      )
      .map((entry) => ({
        key: entry.priority,
        label: entry.priority,
        value: entry.count,
        color: PRIORITY_COLORS[entry.priority] ?? '#64748b'
      }))
  );

  readonly trendLabels = computed(() => (this.data()?.trend ?? []).map((point) => shortDate(point.date)));

  readonly trendSeries = computed<LineSeries[]>(() => {
    const points = this.data()?.trend ?? [];
    return [
      { key: 'created', label: 'Created', color: '#64748b', dashed: true, data: points.map((point) => point.created) },
      { key: 'completed', label: 'Completed', color: '#15803d', data: points.map((point) => point.completed) }
    ];
  });

  readonly projectItems = computed<ProgressItem[]>(() =>
    (this.data()?.projects ?? []).map((project) => ({
      key: project.projectId,
      label: project.name,
      percent: project.progressPercent,
      summary: `${project.completedTasks} of ${project.totalTasks} of your tasks done`,
      note: project.latestDeadline
        ? `Next open deadline ${new Date(project.latestDeadline).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })}`
        : 'No open deadline'
    }))
  );

  ngOnInit(): void {
    this.analytics.refresh();
  }

  setPeriod(period: EmployeePeriod): void {
    this.analytics.setPeriod(period);
  }

  retry(): void {
    this.analytics.refresh();
  }

  deadlineLabel(row: EmployeeDeadline): string {
    if (row.isOverdue) {
      return row.daysOverdue === 0 ? 'Overdue' : `${row.daysOverdue} ${row.daysOverdue === 1 ? 'day' : 'days'} overdue`;
    }
    if (row.daysRemaining <= 0) {
      return 'Due today';
    }
    return row.daysRemaining === 1 ? 'Due tomorrow' : `In ${row.daysRemaining} days`;
  }

  openTasks(target: DrillTarget): void {
    const id = this.user()?.id;
    if (id === undefined) {
      return;
    }
    void this.router.navigate(['/tasks'], { queryParams: employeeDrillQuery(id, target) });
  }

  onTrendPoint(click: PointClick): void {
    const point = this.data()?.trend[click.index];
    if (point) {
      this.openTasks({
        bucket: 'All',
        range: { from: point.date, to: point.date, basis: click.seriesKey === 'completed' ? 'Completed' : 'Created' }
      });
    }
  }
}

function bucketField(key: TaskBucket): 'completed' | 'inProgress' | 'pending' | 'overdue' {
  switch (key) {
    case 'Completed':
      return 'completed';
    case 'InProgress':
      return 'inProgress';
    case 'Overdue':
      return 'overdue';
    default:
      return 'pending';
  }
}
