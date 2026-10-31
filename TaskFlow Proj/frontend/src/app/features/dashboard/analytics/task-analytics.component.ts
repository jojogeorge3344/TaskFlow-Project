import { Component, computed, input, output } from '@angular/core';
import { addDays, shortDate } from '../../../core/analytics/analytics-filter';
import { DrillTarget } from '../../../core/analytics/drill-down';
import {
  AnalyticsBasis,
  AnalyticsTrend,
  BucketCounts,
  DeadlineBuckets,
  DrillBucket,
  DrillDeadlineBucket,
  PriorityCount,
  TaskPriority
} from '../../../core/models';
import { BUCKET_SERIES, PRIORITY_COLORS, percentOf } from '../../../shared/charts/chart-colors';
import { DonutChartComponent } from '../../../shared/charts/donut-chart.component';
import { DonutSlice } from '../../../shared/charts/donut-chart.model';
import { LineChartComponent } from '../../../shared/charts/line-chart.component';
import { LineSeries, PointClick } from '../../../shared/charts/line-chart.model';
import { DeadlineRow } from './task-analytics.model';

@Component({
  selector: 'tf-task-analytics',
  standalone: true,
  imports: [DonutChartComponent, LineChartComponent],
  template: `
    <div class="analytics-grid">
      <article class="analytics-card" aria-labelledby="task-status-title">
        <h2 id="task-status-title">Task status &amp; priority</h2>
        <p class="analytics-card__hint">Share of {{ status()?.total ?? 0 }} tasks &middot; click a slice to see those tasks</p>
        <tf-donut-chart
          [slices]="statusSlices()"
          [loading]="loading()"
          ariaLabel="Tasks by status"
          emptyText="No tasks match these filters."
          (sliceClick)="drill.emit({ bucket: $any($event.key) })"
        />
        <hr class="analytics-card__divider" />
        <tf-donut-chart
          [slices]="prioritySlices()"
          [loading]="loading()"
          ariaLabel="Tasks by priority"
          centerLabel="by priority"
          emptyText="No tasks match these filters."
          (sliceClick)="drill.emit({ priority: $any($event.key), bucket: 'All' })"
        />
      </article>

      <article class="analytics-card" aria-labelledby="task-trend-title">
        <h2 id="task-trend-title">Completion trend</h2>
        <p class="analytics-card__hint">
          Tasks created vs completed per {{ trend()?.granularity ?? 'day' }}, for the selected period &mdash; independent of the date basis
          ({{ basis() }})
        </p>
        <tf-line-chart
          [labels]="trendLabels()"
          [series]="trendSeries()"
          [loading]="loading()"
          ariaLabel="Tasks created and completed over time"
          emptyText="No tasks were created or completed in this period."
          (pointClick)="onTrendPoint($event)"
        />
      </article>

      <article class="analytics-card" aria-labelledby="task-deadline-title">
        <h2 id="task-deadline-title">Deadlines</h2>
        <p class="analytics-card__hint">Open tasks by deadline</p>
        @if (loading()) {
          <div class="tf-skeleton deadline-skeleton" role="status" aria-label="Loading deadlines"></div>
        } @else {
          <ul class="deadline-list">
            @for (row of deadlineRows(); track row.key) {
              <li class="deadline-row">
                <span class="deadline-row__label">{{ row.label }}</span>
                <span class="deadline-row__bar">
                  <button
                    type="button"
                    class="deadline-row__fill"
                    [class.deadline-row__fill--overdue]="row.key === 'Overdue'"
                    [style.width.%]="row.width"
                    [disabled]="row.value === 0"
                    [attr.aria-label]="row.label + ': ' + row.value + ' tasks'"
                    (click)="drill.emit({ deadlineBucket: row.key })"
                  ></button>
                </span>
                <b class="deadline-row__value">{{ row.value }}</b>
              </li>
            }
          </ul>
        }
      </article>
    </div>
  `
})
export class TaskAnalyticsComponent {
  readonly status = input<BucketCounts | null>(null);
  readonly priorities = input<PriorityCount[]>([]);
  readonly deadlines = input<DeadlineBuckets | null>(null);
  readonly trend = input<AnalyticsTrend | null>(null);
  readonly basis = input<AnalyticsBasis>('Created');
  readonly range = input<{ from: string; to: string }>({ from: '', to: '' });
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  readonly statusSlices = computed<DonutSlice[]>(() => {
    const counts = this.status();
    return BUCKET_SERIES.map((series) => ({
      key: series.key,
      label: series.label,
      color: series.color,
      value: counts ? counts[bucketField(series.key as DrillBucket)] : 0
    }));
  });

  readonly prioritySlices = computed<DonutSlice[]>(() =>
    [...this.priorities()]
      .sort((first, second) => priorityRank(second.priority) - priorityRank(first.priority))
      .map((entry) => ({
        key: entry.priority,
        label: entry.priority,
        value: entry.count,
        color: PRIORITY_COLORS[entry.priority] ?? '#64748b'
      }))
  );

  readonly trendLabels = computed(() => (this.trend()?.points ?? []).map((point) => shortDate(point.date)));

  readonly trendSeries = computed<LineSeries[]>(() => {
    const points = this.trend()?.points ?? [];
    return [
      { key: 'created', label: 'Created', color: '#64748b', dashed: true, data: points.map((point) => point.created) },
      { key: 'completed', label: 'Completed', color: '#15803d', data: points.map((point) => point.completed) }
    ];
  });

  readonly deadlineRows = computed<DeadlineRow[]>(() => {
    const counts = this.deadlines();
    const rows: { key: DrillDeadlineBucket; label: string; value: number }[] = [
      { key: 'Overdue', label: 'Overdue', value: counts?.overdue ?? 0 },
      { key: 'DueToday', label: 'Due today', value: counts?.dueToday ?? 0 },
      { key: 'Next7Days', label: 'Next 7 days', value: counts?.next7Days ?? 0 },
      { key: 'Later', label: 'Later', value: counts?.later ?? 0 }
    ];
    const largest = Math.max(1, ...rows.map((row) => row.value));
    return rows.map((row) => ({ ...row, width: percentOf(row.value, largest) }));
  });

  onTrendPoint(click: PointClick): void {
    const point = this.trend()?.points[click.index];
    if (!point) {
      return;
    }
    const { from, to } = this.range();
    const weekly = this.trend()?.granularity === 'week';
    const start = weekly && point.date < from ? from : point.date;
    const lastDay = weekly ? addDays(point.date, 6) : point.date;
    this.drill.emit({
      bucket: 'All',
      range: {
        from: start,
        to: weekly && lastDay > to ? to : lastDay,
        basis: click.seriesKey === 'completed' ? 'Completed' : 'Created'
      }
    });
  }
}

function bucketField(key: DrillBucket): 'completed' | 'inProgress' | 'pending' | 'overdue' {
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

const PRIORITY_ORDER: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

function priorityRank(priority: string): number {
  return PRIORITY_ORDER.indexOf(priority as TaskPriority);
}
