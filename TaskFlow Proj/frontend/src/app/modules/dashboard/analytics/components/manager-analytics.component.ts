import { Component, input, output } from '@angular/core';
import { DrillTarget } from '../../../../shared/utils/drill-down';
import { ManagerAnalytics } from '../models/analytics.model';

@Component({
  selector: 'tf-manager-analytics',
  standalone: true,
  template: `
    <article class="analytics-card" aria-labelledby="manager-analytics-title">
      <h2 id="manager-analytics-title">Manager-wise teams</h2>
      <p class="analytics-card__hint">Team = direct reports plus members of the departments a manager runs</p>
      @if (loading()) {
        <div class="tf-skeleton manager-skeleton" role="status" aria-label="Loading managers"></div>
      } @else if (managers().length === 0) {
        <p class="analytics-card__empty">No managers match these filters.</p>
      } @else {
        <div class="analytics-table-wrap">
          <table class="analytics-table">
            <thead>
              <tr>
                <th scope="col">Manager</th>
                <th scope="col" class="num">Depts</th>
                <th scope="col" class="num">Staff</th>
                <th scope="col" class="num">Projects</th>
                <th scope="col" class="num">Tasks</th>
                <th scope="col" class="num">Pending</th>
                <th scope="col" class="num">In progress</th>
                <th scope="col" class="num">Done</th>
                <th scope="col" class="num">Overdue</th>
              </tr>
            </thead>
            <tbody>
              @for (manager of managers(); track manager.managerId) {
                <tr>
                  <th scope="row">{{ manager.name }}</th>
                  <td class="num">{{ manager.departments }}</td>
                  <td class="num">{{ manager.employees }}</td>
                  <td class="num">{{ manager.projects }}</td>
                  <td class="num">
                    <button type="button" class="cell-button" [disabled]="manager.tasks.total === 0" (click)="drill.emit({ managerId: manager.managerId, bucket: 'All' })">
                      {{ manager.tasks.total }}
                    </button>
                  </td>
                  <td class="num">{{ manager.tasks.pending }}</td>
                  <td class="num">{{ manager.tasks.inProgress }}</td>
                  <td class="num">{{ manager.tasks.completionPercent }}%</td>
                  <td class="num">
                    <button
                      type="button"
                      class="cell-button cell-button--overdue"
                      [disabled]="manager.tasks.overdue === 0"
                      [attr.aria-label]="manager.name + ' overdue tasks: ' + manager.tasks.overdue"
                      (click)="drill.emit({ managerId: manager.managerId, bucket: 'Overdue' })"
                    >
                      {{ manager.tasks.overdue }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </article>
  `
})
export class ManagerAnalyticsComponent {
  readonly managers = input<ManagerAnalytics[]>([]);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();
}
