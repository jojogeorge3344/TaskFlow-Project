import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { BASIS_LABELS, RANGE_PRESETS, detectPreset, shortDate } from '../../../shared/utils/analytics-filter';
import { DrillTarget, drillDownQuery } from '../../../shared/utils/drill-down';
import { AuthService } from '../../../shared/auth/auth.service';
import { Permissions } from '../../../shared/auth/permissions';
import { AnalyticsFilterState } from '../analytics/models/analytics.model';
import { ManagerAnalyticsService } from '../analytics/services/manager-analytics.service';
import { provideDashboardCharts } from '../utils/chart-providers';
import { KpiCardComponent } from '../components/charts/kpi-card.component';
import { DashboardFilterBarComponent } from '../components/dashboard-filter-bar.component';
import { ProjectAnalyticsComponent } from '../analytics/components/project-analytics.component';
import { TeamAttentionComponent } from '../analytics/components/team-attention.component';
import { TeamEmployeeAnalyticsComponent } from '../analytics/components/team-employee-analytics.component';
import { TeamTaskAnalyticsComponent } from '../analytics/components/team-task-analytics.component';

@Component({
  selector: 'app-manager-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    DashboardFilterBarComponent,
    KpiCardComponent,
    TeamAttentionComponent,
    TeamTaskAnalyticsComponent,
    TeamEmployeeAnalyticsComponent,
    ProjectAnalyticsComponent
  ],
  providers: [provideDashboardCharts()],
  templateUrl: './manager-dashboard.component.html'
})
export class ManagerDashboardComponent implements OnInit {
  private readonly analytics = inject(ManagerAnalyticsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;

  /** The filter bar is part of "use dashboard filters"; the analytics themselves need "view charts and reports". */
  readonly canFilter = computed(() => this.user()?.permissions.includes(Permissions.DashboardFilters) ?? false);

  readonly filter = this.analytics.filter;
  readonly options = this.analytics.options;
  readonly data = this.analytics.data;
  readonly loading = this.analytics.loading;
  readonly error = this.analytics.error;
  readonly today = this.analytics.today;

  /** Administrators hold the manager dashboard permission too but have no team of their own. */
  readonly isAdministrator = computed(() => this.user()?.permissions.includes(Permissions.DashboardViewOrganization) ?? false);

  /** The manager has no one on their team yet, so no filter could ever match. */
  readonly noTeam = computed(() => !this.loading() && !this.error() && this.data()?.summary.teamSize === 0);

  readonly isEmpty = computed(
    () => !this.loading() && !this.error() && !this.noTeam() && this.data()?.tasksByStatus.total === 0
  );

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
