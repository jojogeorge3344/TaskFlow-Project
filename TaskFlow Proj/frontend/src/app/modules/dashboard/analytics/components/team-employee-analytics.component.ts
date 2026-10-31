import { Component, input, output } from '@angular/core';
import { DrillTarget } from '../../../../shared/utils/drill-down';
import { DrillBucket, TeamEmployee } from '../models/analytics.model';
import { BUCKET_SERIES, bucketValues } from '../../../../shared/utils/chart-colors';

@Component({
  selector: 'tf-team-employee-analytics',
  standalone: true,
  template: `
    <article class="analytics-card" aria-labelledby="team-employee-title">
      <h2 id="team-employee-title">Employee-wise workload</h2>
      <p class="analytics-card__hint">
        High workload = open tasks at least 1.5&times; the team average and at least 5. Overdue = any overdue task.
      </p>
      @if (loading()) {
        <div class="tf-skeleton team-state" role="status" aria-label="Loading employees"></div>
      } @else if (employees().length === 0) {
        <p class="team-state team-state--empty">
          No team members to show. Ask an administrator to assign departments or direct reports to you.
        </p>
      } @else {
        <div class="team-table-wrap">
          <table class="team-table">
            <thead>
              <tr>
                <th scope="col">Employee</th>
                <th scope="col" class="r">Open</th>
                <th scope="col" class="r">Done</th>
                <th scope="col" class="r">Overdue</th>
                <th scope="col" class="r">Completion</th>
                <th scope="col">Flag</th>
                <th scope="col">Tasks by status</th>
              </tr>
            </thead>
            <tbody>
              @for (employee of employees(); track employee.userId) {
                <tr [attr.data-employee]="employee.userId">
                  <th scope="row">{{ employee.name }}</th>
                  <td class="r">{{ employee.tasks.open }}</td>
                  <td class="r">{{ employee.tasks.completed }}</td>
                  <td class="r">{{ employee.tasks.overdue }}</td>
                  <td class="r">{{ employee.tasks.completionPercent }}%</td>
                  <td class="flags">
                    @if (employee.highWorkload) {
                      <span class="flag flag--workload" data-flag="high-workload">High workload</span>
                    }
                    @if (employee.hasOverdue) {
                      <span class="flag flag--overdue" data-flag="overdue">Overdue: {{ employee.tasks.overdue }}</span>
                    }
                    @if (!employee.highWorkload && !employee.hasOverdue) {
                      <span class="flag-none" aria-label="No flags">&mdash;</span>
                    }
                  </td>
                  <td>
                    @if (employee.tasks.total === 0) {
                      <span class="flag-none">No tasks</span>
                    } @else {
                      <div class="seg-bar" role="group" [attr.aria-label]="employee.name + ' tasks by status'">
                        @for (item of series; track item.key; let index = $index) {
                          @if (values(employee)[index] > 0) {
                            <button
                              type="button"
                              class="seg"
                              [style.flex-grow]="values(employee)[index]"
                              [style.background]="item.color"
                              [attr.data-segment]="item.key"
                              [attr.aria-label]="employee.name + ' ' + item.label + ': ' + values(employee)[index]"
                              [title]="item.label + ': ' + values(employee)[index]"
                              (click)="drill.emit({ employeeId: employee.userId, bucket: bucket(item.key) })"
                            >
                              {{ values(employee)[index] }}
                            </button>
                          }
                        }
                      </div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </article>
  `,
  styles: `
    .team-table-wrap {
      overflow-x: auto;
    }
    .team-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .team-table th,
    .team-table td {
      padding: var(--tf-space-2);
      border-bottom: 1px solid var(--tf-border);
      text-align: left;
      vertical-align: middle;
    }
    .team-table thead th {
      color: var(--tf-muted);
      font-weight: 600;
      white-space: nowrap;
    }
    .team-table tbody th {
      font-weight: 600;
    }
    .r {
      text-align: right !important;
    }
    .flags {
      white-space: nowrap;
    }
    .flag {
      display: inline-block;
      margin-right: var(--tf-space-1);
      padding: 0.1rem 0.5rem;
      border: 1px solid currentColor;
      border-radius: 99px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .flag--workload {
      color: #92400e;
      background: #fef3c7;
    }
    .flag--overdue {
      color: #991b1b;
      background: #fee2e2;
    }
    .flag-none {
      color: var(--tf-muted);
    }
    .seg-bar {
      display: flex;
      min-width: 9rem;
      height: 1.5rem;
      border-radius: var(--tf-radius-sm);
      overflow: hidden;
    }
    .seg {
      flex: 1 1 0;
      min-width: 1.25rem;
      border: 0;
      color: #fff;
      cursor: pointer;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .team-state {
      display: grid;
      min-height: 6rem;
      margin: 0;
      place-items: center;
    }
    .team-state--empty {
      color: var(--tf-muted);
      text-align: center;
    }
  `
})
export class TeamEmployeeAnalyticsComponent {
  readonly employees = input<TeamEmployee[]>([]);
  readonly loading = input(false);

  readonly drill = output<DrillTarget>();

  readonly series = BUCKET_SERIES;

  values(employee: TeamEmployee): number[] {
    return bucketValues(employee.tasks);
  }

  bucket(key: string): DrillBucket {
    return key as DrillBucket;
  }
}
