import { Component, computed, input, output } from '@angular/core';
import { DrillTarget } from '../../../core/analytics/drill-down';
import { ProjectAnalytics } from '../../../core/models';
import { ProgressListComponent } from '../../../shared/charts/progress-list.component';
import { ProgressItem } from '../../../shared/charts/progress-list.model';

@Component({
  selector: 'tf-project-analytics',
  standalone: true,
  imports: [ProgressListComponent],
  template: `
    <article class="analytics-card" aria-labelledby="project-analytics-title">
      <h2 id="project-analytics-title">Project-wise progress</h2>
      <p class="analytics-card__hint">Projected end = latest task deadline (projects have no deadline of their own)</p>
      <tf-progress-list
        [items]="items()"
        [loading]="loading()"
        emptyText="No projects match these filters."
        (itemClick)="drill.emit({ projectId: $any($event.key), bucket: 'All' })"
      />
    </article>
  `
})
export class ProjectAnalyticsComponent {
  readonly projects = input<ProjectAnalytics[]>([]);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  readonly items = computed<ProgressItem[]>(() =>
    this.projects().map((project) => ({
      key: project.projectId,
      label: project.name,
      percent: project.tasks.completionPercent,
      summary: `${project.outstanding} open · ${project.tasks.overdue} overdue`,
      note: project.projectedEnd
        ? `Projected end ${new Date(project.projectedEnd).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })}`
        : 'No end date'
    }))
  );
}
