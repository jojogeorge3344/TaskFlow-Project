import { Component, computed, input, output } from '@angular/core';
import { DrillTarget } from '../../../../shared/utils/drill-down';
import { BucketCounts, DrillBucket, PriorityCount } from '../models/analytics.model';
import { TaskPriority } from '../../../monitoring/task-management/models/task.model';
import { BUCKET_SERIES, PRIORITY_COLORS } from '../../utils/chart-colors';
import { DonutChartComponent } from '../../components/charts/donut-chart.component';
import { DonutSlice } from '../../components/charts/donut-chart.model';

const PRIORITY_ORDER: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

@Component({
  selector: 'tf-team-task-analytics',
  standalone: true,
  imports: [DonutChartComponent],
  template: `
    <article class="analytics-card" aria-labelledby="team-task-title">
      <h2 id="team-task-title">Task status &amp; priority</h2>
      <p class="analytics-card__hint">Share of {{ status()?.total ?? 0 }} team tasks &middot; click a slice to see those tasks</p>
      <tf-donut-chart
        [slices]="statusSlices()"
        [loading]="loading()"
        ariaLabel="Team tasks by status"
        emptyText="No tasks match these filters."
        (sliceClick)="drill.emit({ bucket: $any($event.key) })"
      />
      <hr class="analytics-card__divider" />
      <tf-donut-chart
        [slices]="prioritySlices()"
        [loading]="loading()"
        ariaLabel="Team tasks by priority"
        centerLabel="by priority"
        emptyText="No tasks match these filters."
        (sliceClick)="drill.emit({ priority: $any($event.key), bucket: 'All' })"
      />
    </article>
  `
})
export class TeamTaskAnalyticsComponent {
  readonly status = input<BucketCounts | null>(null);
  readonly priorities = input<PriorityCount[]>([]);
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
      .sort((first, second) => rank(second.priority) - rank(first.priority))
      .map((entry) => ({
        key: entry.priority,
        label: entry.priority,
        value: entry.count,
        color: PRIORITY_COLORS[entry.priority] ?? '#64748b'
      }))
  );
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

function rank(priority: string): number {
  return PRIORITY_ORDER.indexOf(priority as TaskPriority);
}
