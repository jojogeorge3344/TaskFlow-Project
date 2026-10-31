import { Component, computed, input, output } from '@angular/core';
import { DrillTarget } from '../../../../shared/utils/drill-down';
import { DepartmentAnalytics, DrillBucket } from '../models/analytics.model';
import { BUCKET_SERIES, bucketValues } from '../../utils/chart-colors';
import { StackedBarChartComponent } from '../../components/charts/stacked-bar-chart.component';
import { SegmentClick, StackedRow } from '../../components/charts/stacked-bar-chart.model';

@Component({
  selector: 'tf-department-analytics',
  standalone: true,
  imports: [StackedBarChartComponent],
  template: `
    <article class="analytics-card" aria-labelledby="department-analytics-title">
      <h2 id="department-analytics-title">Department-wise tasks</h2>
      <p class="analytics-card__hint">Stacked by status &middot; click a segment to see those tasks</p>
      <tf-stacked-bar-chart
        [rows]="rows()"
        [series]="series"
        [loading]="loading()"
        rowHeading="Department"
        ariaLabel="Tasks per department by status"
        emptyText="No tasks match these filters."
        (segmentClick)="onSegment($event)"
      />
      <p class="analytics-card__note">
        Departments come from each task's assignee. Tasks of people without a department are grouped separately so totals add up.
      </p>
    </article>
  `
})
export class DepartmentAnalyticsComponent {
  readonly departments = input<DepartmentAnalytics[]>([]);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  readonly series = BUCKET_SERIES;

  readonly rows = computed<StackedRow[]>(() =>
    this.departments().map((department) => ({
      key: department.departmentId,
      label: department.name,
      values: bucketValues(department.tasks),
      percent: department.tasks.completionPercent
    }))
  );

  onSegment(click: SegmentClick): void {
    this.drill.emit({ departmentId: click.rowKey as number | null, bucket: click.seriesKey as DrillBucket });
  }
}
