import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DrillTarget } from '../../../core/analytics/drill-down';
import { DrillBucket, EmployeeAnalyticsSection } from '../../../core/models';
import { BUCKET_SERIES, bucketValues } from '../../../shared/charts/chart-colors';
import { SegmentClick, StackedBarChartComponent, StackedRow } from '../../../shared/charts/stacked-bar-chart.component';

@Component({
  selector: 'tf-employee-analytics',
  standalone: true,
  imports: [StackedBarChartComponent, RouterLink],
  template: `
    <article class="analytics-card" aria-labelledby="employee-analytics-title">
      <h2 id="employee-analytics-title">Employee-wise workload</h2>
      <p class="analytics-card__hint">
        Top {{ section()?.items?.length ?? 0 }} by open tasks
        @if (hasMore()) {
          &middot; <a routerLink="/workload" data-action="view-all">View all {{ section()?.totalEmployees }}</a>
        }
      </p>
      <tf-stacked-bar-chart
        [rows]="rows()"
        [series]="series"
        [loading]="loading()"
        rowHeading="Employee"
        ariaLabel="Tasks per employee by status"
        emptyText="No tasks match these filters."
        (segmentClick)="onSegment($event)"
      />
      <p class="analytics-card__note">
        A task shared with recipients counts for each person, so employee totals can be higher than the number of tasks.
      </p>
    </article>
  `
})
export class EmployeeAnalyticsComponent {
  readonly section = input<EmployeeAnalyticsSection | null>(null);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  readonly series = BUCKET_SERIES;

  readonly hasMore = computed(() => {
    const section = this.section();
    return !!section && section.totalEmployees > section.items.length;
  });

  readonly rows = computed<StackedRow[]>(() =>
    (this.section()?.items ?? []).map((employee) => ({
      key: employee.userId,
      label: employee.name,
      values: bucketValues(employee.tasks),
      percent: employee.tasks.completionPercent
    }))
  );

  onSegment(click: SegmentClick): void {
    this.drill.emit({ employeeId: click.rowKey as number, bucket: click.seriesKey as DrillBucket });
  }
}
