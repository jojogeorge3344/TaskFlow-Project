import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { User } from '../models/user.model';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { roleBadgeClass } from '../../../../shared/utils/role-badge';
import { allocationsText } from '../../../../shared/utils/allocation-format';
import { initials } from '../../../../shared/utils/initials';

/** Read-only employee details shown inside the side panel. */
@Component({
  selector: 'app-employee-details-panel',
  standalone: true,
  imports: [DatePipe, IconComponent],
  template: `
    <div class="tf-drawer__body">
      <header class="tf-identity">
        <span class="tf-avatar tf-avatar--lg tf-avatar--brand" aria-hidden="true">{{ avatar() }}</span>
        <div class="tf-identity__text">
          <div class="tf-identity__name">{{ employee().fullName }}</div>
          <div class="tf-identity__sub">{{ employee().email }}</div>
        </div>
      </header>

      <dl class="details__list tf-section">
        <dt>Employee code</dt>
        <dd>{{ employee().employeeCode || '—' }}</dd>
        <dt>Username</dt>
        <dd>{{ employee().username }}</dd>
        <dt>Email</dt>
        <dd>{{ employee().email }}</dd>
        <dt>Role</dt>
        <dd><span [class]="roleBadgeClass(employee().role)">{{ employee().role }}</span></dd>
        <dt>Projects</dt>
        <dd data-field="projects">{{ projectsText() }}</dd>
        <dt>Departments</dt>
        <dd data-field="departments">{{ departmentsText() }}</dd>
        <dt>Managers</dt>
        <dd data-field="managers">{{ managersText() }}</dd>
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
        <dt>Two-step</dt>
        <dd data-field="mfa">{{ employee().mfaEnabled ? 'On (authenticator app)' : 'Off' }}</dd>
        <dt>Last login</dt>
        <dd>{{ employee().lastLoginAt ? (employee().lastLoginAt | date: 'medium') : 'Never' }}</dd>
      </dl>
    </div>

    <div class="tf-drawer__foot">
      @if (canResetMfa() && employee().mfaEnabled) {
        <button type="button" class="tf-btn tf-btn--ghost" data-action="reset-mfa" (click)="resetMfa.emit()">
          Reset MFA
        </button>
      }
      <button type="button" class="tf-btn tf-btn--ghost" (click)="closed.emit()">Close</button>
      <button type="button" class="tf-btn tf-btn--primary" (click)="edit.emit()">
        <tf-icon name="edit" />
        Edit
      </button>
    </div>
  `,
  styles: `
    .details__list {
      grid-template-columns: 8rem minmax(0, 1fr);
      gap: 0.75rem 1rem;
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
  /** Whether the viewer may remove this user's MFA (employee management). The API enforces it too. */
  readonly canResetMfa = input(false);
  readonly edit = output<void>();
  readonly resetMfa = output<void>();
  readonly closed = output<void>();

  readonly avatar = computed(() => initials(this.employee().fullName));

  readonly projectsText = computed(() => {
    const user = this.employee();
    return user.projectAllocations.length > 0
      ? `${allocationsText(user.projectAllocations)} · ${user.totalAllocatedPercent}% allocated`
      : 'Unassigned';
  });

  readonly departmentsText = computed(() => this.employee().departments.map((d) => d.name).join(', ') || 'Unassigned');

  readonly managersText = computed(() => this.employee().managers.map((m) => m.fullName).join(', ') || 'None');

  readonly managedProjects = computed(
    () => (this.employee().managerProjects ?? []).map((project) => project.name).join(', ') || 'None'
  );

  readonly team = computed(
    () => (this.employee().reports ?? []).map((report) => report.fullName).join(', ') || 'No one yet'
  );
}
