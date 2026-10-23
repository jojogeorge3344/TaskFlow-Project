import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DrillTarget } from '../../../core/analytics/drill-down';
import { TeamAttention } from '../../../core/models';

@Component({
  selector: 'tf-team-attention',
  standalone: true,
  imports: [RouterLink],
  template: `
    <article class="analytics-card" aria-labelledby="team-attention-title">
      <h2 id="team-attention-title">Team performance summary</h2>
      <p class="analytics-card__hint">Delayed work that needs attention</p>
      @if (loading()) {
        <div class="tf-skeleton attention-state" role="status" aria-label="Loading summary"></div>
      } @else if (isClear()) {
        <p class="attention-state attention-state--empty" data-attention-empty>Nothing is overdue for the team.</p>
      } @else {
        <h3 class="attention-heading">Most overdue tasks</h3>
        <ul class="attention-list">
          @for (task of attention()?.delayedTasks ?? []; track task.taskId) {
            <li>
              <a class="attention-item" [routerLink]="['/tasks', task.taskId]" data-delayed-task>
                <span>
                  <b>{{ task.taskCode }}</b> {{ task.title }}<br />
                  <small>{{ task.assigneeName }}@if (task.projectName) { &middot; {{ task.projectName }} }</small>
                </span>
                <span class="attention-badge">{{ overdueText(task.daysOverdue) }}</span>
              </a>
            </li>
          }
        </ul>
        @if ((attention()?.projectsNeedingAttention?.length ?? 0) > 0) {
          <h3 class="attention-heading">Projects needing attention</h3>
          <ul class="attention-list">
            @for (project of attention()?.projectsNeedingAttention ?? []; track project.projectId) {
              <li>
                <button
                  type="button"
                  class="attention-item"
                  data-attention-project
                  (click)="drill.emit({ projectId: project.projectId, bucket: 'Overdue' })"
                >
                  <span>
                    <b>{{ project.name }}</b>
                  </span>
                  <span class="attention-badge">{{ project.overdueTasks }} overdue</span>
                </button>
              </li>
            }
          </ul>
        }
      }
    </article>
  `,
  styles: `
    .attention-heading {
      margin: var(--tf-space-3) 0 var(--tf-space-1);
      color: var(--tf-muted);
      font-size: 0.8125rem;
      font-weight: 600;
    }
    .attention-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .attention-item {
      display: flex;
      width: 100%;
      align-items: center;
      justify-content: space-between;
      gap: var(--tf-space-2);
      padding: var(--tf-space-2) var(--tf-space-1);
      background: none;
      border: 0;
      border-bottom: 1px solid var(--tf-border);
      color: inherit;
      cursor: pointer;
      font: inherit;
      text-align: left;
      text-decoration: none;
    }
    .attention-item:hover {
      background: var(--tf-primary-soft);
    }
    small {
      color: var(--tf-muted);
    }
    .attention-badge {
      flex: none;
      padding: 0.1rem 0.5rem;
      border: 1px solid #991b1b;
      border-radius: 99px;
      background: #fee2e2;
      color: #991b1b;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .attention-state {
      display: block;
      min-height: 4rem;
      margin: 0;
    }
    .attention-state--empty {
      display: grid;
      color: var(--tf-muted);
      place-items: center;
      text-align: center;
    }
  `
})
export class TeamAttentionComponent {
  readonly attention = input<TeamAttention | null>(null);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  isClear(): boolean {
    const attention = this.attention();
    return !attention || (attention.delayedTasks.length === 0 && attention.projectsNeedingAttention.length === 0);
  }

  overdueText(days: number): string {
    if (days <= 0) {
      return 'Overdue today';
    }
    return days === 1 ? '1 day overdue' : `${days} days overdue`;
  }
}
