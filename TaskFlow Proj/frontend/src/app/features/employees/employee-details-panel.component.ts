import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { User } from '../../core/models';
import { IconComponent } from '../../core/layout/icon.component';
import { roleBadgeClass } from './role-badge';
import { initials } from '../../shared/ui/initials';

/** Read-only employee details shown inside the side panel. */
@Component({
  selector: 'app-employee-details-panel',
  standalone: true,
  imports: [DatePipe, IconComponent],
  template: `
    <div class="tf-drawer__body">
      <div class="details__hero">
        <span class="tf-avatar tf-avatar--lg" aria-hidden="true">{{ avatar() }}</span>
        <div>
          <div class="details__name">{{ employee().fullName }}</div>
          <div class="details__sub">{{ employee().email }}</div>
        </div>
      </div>

      <dl class="details__list">
        <dt>Employee code</dt>
        <dd>{{ employee().employeeCode || '—' }}</dd>
        <dt>Username</dt>
        <dd>{{ employee().username }}</dd>
        <dt>Email</dt>
        <dd>{{ employee().email }}</dd>
        <dt>Role</dt>
        <dd><span [class]="roleBadgeClass(employee().role)">{{ employee().role }}</span></dd>
        <dt>Project</dt>
        <dd>{{ employee().projectName || 'Unassigned' }}</dd>
        <dt>Department</dt>
        <dd data-field="department">{{ employee().departmentName || 'Unassigned' }}</dd>
        <dt>Manager</dt>
        <dd data-field="manager">{{ employee().managerName || 'None' }}</dd>
        @if (employee().role === 'Manager') {
          <dt>Manages projects</dt>
          <dd data-field="managed-projects">{{ managedProjects() }}</dd>
          <dt>Team</dt>
          <dd data-field="team">{{ team() }}</dd>
        }
        <dt>Status</dt>
        <dd>
          <span class="tf-badge" [class.tf-badge--active]="employee().isActive" [class.tf-badge--inactive]="!employee().isActive">
            {{ employee().isActive ? 'Active' : 'Inactive' }}
          </span>
        </dd>
        <dt>Created</dt>
        <dd>{{ employee().createdAt | date: 'mediumDate' }}</dd>
        <dt>Last login</dt>
        <dd>{{ employee().lastLoginAt ? (employee().lastLoginAt | date: 'medium') : 'Never' }}</dd>
      </dl>
    </div>

    <div class="tf-drawer__foot">
      <button type="button" class="tf-btn tf-btn--ghost" (click)="closed.emit()">Close</button>
      <button type="button" class="tf-btn tf-btn--primary" (click)="edit.emit()">
        <tf-icon name="edit" />
        Edit
      </button>
    </div>
  `,
  styles: `
    .details__hero {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .details__name {
      font-size: 1rem;
      font-weight: 600;
    }

    .details__sub {
      color: var(--tf-muted);
      overflow-wrap: anywhere;
    }

    .details__list {
      display: grid;
      grid-template-columns: 8rem 1fr;
      gap: 0.625rem 1rem;
      margin: 0;
    }

    .details__list dt {
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }

    .details__list dd {
      margin: 0;
      font-weight: 500;
      overflow-wrap: anywhere;
    }
  `
})
export class EmployeeDetailsPanelComponent {
  readonly roleBadgeClass = roleBadgeClass;

  readonly employee = input.required<User>();
  readonly edit = output<void>();
  readonly closed = output<void>();

  readonly avatar = computed(() => initials(this.employee().fullName));

  readonly managedProjects = computed(
    () => (this.employee().managerProjects ?? []).map((project) => project.name).join(', ') || 'None'
  );

  readonly team = computed(
    () => (this.employee().reports ?? []).map((report) => report.fullName).join(', ') || 'No one yet'
  );
}
