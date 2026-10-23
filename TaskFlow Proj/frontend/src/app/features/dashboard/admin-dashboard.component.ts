import { Component, OnInit, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BASIS_LABELS, RANGE_PRESETS, detectPreset, shortDate } from '../../core/analytics/analytics-filter';
import { DrillTarget, drillDownQuery } from '../../core/analytics/drill-down';
import { AuthService } from '../../core/auth/auth.service';
import { AnalyticsFilterState } from '../../core/models';
import { DashboardAnalyticsService } from '../../core/services/dashboard-analytics.service';
import { provideDashboardCharts } from '../../shared/charts/chart-providers';
import { KpiCardComponent } from '../../shared/charts/kpi-card.component';
import { DashboardFilterBarComponent } from '../../shared/dashboard-filter-bar/dashboard-filter-bar.component';
import { DepartmentAnalyticsComponent } from './analytics/department-analytics.component';
import { EmployeeAnalyticsComponent } from './analytics/employee-analytics.component';
import { ManagerAnalyticsComponent } from './analytics/manager-analytics.component';
import { ProjectAnalyticsComponent } from './analytics/project-analytics.component';
import { TaskAnalyticsComponent } from './analytics/task-analytics.component';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    DashboardFilterBarComponent,
    KpiCardComponent,
    DepartmentAnalyticsComponent,
    TaskAnalyticsComponent,
    EmployeeAnalyticsComponent,
    ManagerAnalyticsComponent,
    ProjectAnalyticsComponent
  ],
  providers: [provideDashboardCharts()],
  templateUrl: './admin-dashboard.component.html'
})
export class AdminDashboardComponent implements OnInit {
  private readonly analytics = inject(DashboardAnalyticsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;

  readonly filter = this.analytics.filter;
  readonly options = this.analytics.options;
  readonly data = this.analytics.data;
  readonly loading = this.analytics.loading;
  readonly error = this.analytics.error;
  readonly today = this.analytics.today;

  readonly isEmpty = computed(() => !this.loading() && !this.error() && this.data()?.tasksByStatus.total === 0);

  readonly rangeLabel = computed(() => {
    const filter = this.filter();
    const preset = detectPreset(filter, this.today());
    const known = RANGE_PRESETS.find((candidate) => candidate.key === preset);
    return preset === 'custom' || !known ? `${shortDate(filter.from)} – ${shortDate(filter.to)}` : known.label;
  });

  readonly basisLabel = computed(() => BASIS_LABELS[this.filter().basis].toLowerCase());

  ngOnInit(): void {
    this.analytics.start();
  }

  onFilterChange(patch: Partial<AnalyticsFilterState>): void {
    this.analytics.update(patch);
  }

  reset(): void {
    this.analytics.reset();
  }

  retry(): void {
    this.analytics.refresh();
  }

  openTasks(target: DrillTarget): void {
    void this.router.navigate(['/tasks'], { queryParams: drillDownQuery(this.filter(), target) });
  }
}
