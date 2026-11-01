import { Component, ElementRef, computed, inject, output, signal, viewChild } from '@angular/core';
import { MAX_TOTAL_PERCENT, User } from '../../employee-management/models/user.model';
import { Position, ProjectMember } from '../models/project.model';
import { SYSTEM_ROLE_IDS } from '../../../settings/role-configuration/models/role.model';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { PositionService } from '../positions/services/position.service';
import { ProjectService } from '../services/project.service';
import { EmployeePickerComponent } from './employee-picker.component';
import { PositionComboboxComponent } from '../../components/position-combobox.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ToastService } from '../../../../shared/services/toast.service';
import { TeamRow } from './project-team-dialog.model';

/** Assign-team modal: choose employees, the position each holds on the project and their percentage. Opens right after a
 * project is created (and can be skipped) and from the project's Team button later. */
@Component({
  selector: 'app-project-team-dialog',
  standalone: true,
  imports: [IconComponent, EmployeePickerComponent, PositionComboboxComponent],
  template: `
    <dialog #dialog class="tf-modal team-dialog" aria-labelledby="team-dialog-title" (cancel)="onCancelEvent($event)">
      <div>
        <header class="tf-modal__head">
          <h2 id="team-dialog-title">{{ afterCreate() ? 'Assign team' : 'Project team' }}{{ projectName() ? ': ' + projectName() : '' }}</h2>
          <button type="button" class="tf-btn tf-btn--icon" aria-label="Close dialog" (click)="close()">
            <tf-icon name="x" />
          </button>
        </header>

        <div class="tf-modal__body">
          @if (serverError()) {
            <div class="tf-alert" role="alert" data-team-error>{{ serverError() }}</div>
          }
          @if (loadError()) {
            <div class="tf-alert" role="alert">{{ loadError() }}</div>
          } @else if (loading()) {
            <p class="tf-field__hint">Loading the team…</p>
          } @else {
            <p class="tf-field__hint">
              Choose an employee, the position they hold on this project and the share of their time (1 to {{ maxPercent }}%).
              Nobody can be allocated more than {{ maxPercent }}% across all projects. Mark who manages the project:
              everyone on the team then has them as their manager for it.
            </p>

            @if (managerError(); as message) {
              <div class="tf-notice" role="alert" data-manager-error>{{ message }}</div>
            }

            @if (rows().length > 0) {
              <div class="team-head" aria-hidden="true" data-team-head>
                <span>Employee</span>
                <span>Role</span>
                <span class="team-head__percent">Allocation %</span>
                <span>Project manager</span>
                <span></span>
              </div>
            }

            @for (row of rows(); track row.key; let index = $index) {
              <div class="team-row" [attr.data-row]="index">
                <div class="team-cell team-cell--wide" data-label="Employee">
                  <app-employee-picker
                    [selected]="row.userId === null ? null : { id: row.userId, fullName: row.fullName }"
                    [exclude]="chosenUserIds()"
                    [label]="'Employee ' + (index + 1)"
                    [invalid]="submitted() && row.userId === null"
                    (picked)="pick(row.key, $event)"
                  />
                </div>
                <div class="team-cell team-cell--wide" data-label="Role">
                  <app-position-combobox
                    [positions]="positions()"
                    [positionId]="row.positionId"
                    [label]="'Position for employee ' + (index + 1)"
                    placeholder="Role in this project"
                    [invalid]="submitted() && row.positionId === null"
                    (chosen)="update(row.key, { positionId: $event?.id ?? null })"
                    (created)="onPositionCreated($event)"
                  />
                </div>
                <div class="team-cell" data-label="Allocation %">
                  <input
                    class="tf-input team-row__percent"
                    type="number"
                    min="1"
                    [max]="maxPercent"
                    step="1"
                    inputmode="numeric"
                    [attr.aria-label]="'Percentage for employee ' + (index + 1)"
                    [class.is-invalid]="rowError(row) !== null"
                    [value]="row.percent ?? ''"
                    (input)="setPercent(row.key, $any($event.target).value)"
                  />
                </div>
                <label
                  class="team-row__manager"
                  [class.is-disabled]="!row.canManage"
                  [title]="row.canManage ? 'Manages this project' : 'Only a person with the Manager role can manage a project'"
                >
                  <input
                    type="checkbox"
                    data-field="is-manager"
                    [checked]="row.isManager"
                    [disabled]="!row.canManage"
                    [attr.aria-label]="'Project manager: employee ' + (index + 1)"
                    (change)="update(row.key, { isManager: $any($event.target).checked })"
                  />
                  <span class="team-row__manager-text">Manager</span>
                </label>
                <button type="button" class="tf-btn tf-btn--ghost team-row__remove" [attr.aria-label]="'Remove employee ' + (index + 1)" (click)="remove(row.key)">
                  Remove
                </button>
                @if (rowError(row); as message) {
                  <span class="tf-field__error team-row__error" data-row-error>{{ message }}</span>
                }
              </div>
            } @empty {
              <p class="tf-field__hint" data-empty>No one is on this project yet.</p>
            }

            <button type="button" class="tf-btn tf-btn--ghost" data-action="add-member" (click)="add()">Add employee</button>
          }
        </div>

        <footer class="tf-modal__foot">
          <button type="button" class="tf-btn tf-btn--ghost" data-action="skip" (click)="close()">
            {{ afterCreate() ? 'Skip for now' : 'Cancel' }}
          </button>
          <button type="button" class="tf-btn tf-btn--primary" data-action="save-team" [disabled]="saving() || loading() || hasErrors() || managerError() !== null" (click)="save()">
            {{ saving() ? 'Saving…' : 'Save team' }}
          </button>
        </footer>
      </div>
    </dialog>
  `,
  styles: `
    .team-dialog {
      width: min(50rem, 96vw);
      /* One column template for the heading row and every data row, so they cannot drift apart. */
      --team-cols: minmax(0, 1.3fr) minmax(0, 1fr) 6rem 8.5rem 5.5rem;
    }

    .team-head,
    .team-row {
      display: grid;
      grid-template-columns: var(--team-cols);
      gap: 0.5rem;
    }

    .team-head {
      margin-bottom: 0.25rem;
      /* Same inset as a row: its padding plus its 1px border. */
      padding: 0 calc(0.5rem + 1px);
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--tf-text-secondary);
    }

    .team-head span {
      min-width: 0;
      white-space: nowrap;
    }

    .team-head__percent {
      /* Right-aligned like the number it sits above, inset by the input's own padding. */
      padding-right: 0.75rem;
      text-align: right;
    }

    .team-row {
      align-items: start;
      margin-bottom: 0.5rem;
      padding: 0.5rem;
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius-lg);
      background: var(--tf-surface-alt);
    }

    .team-cell {
      min-width: 0;
    }

    .team-cell::before {
      display: none;
    }

    .team-row .tf-input,
    .team-row .tf-select {
      background-color: var(--tf-surface);
    }

    .team-row__percent {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }

    .team-row__error {
      grid-column: 1 / -1;
    }

    .team-row__manager {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      height: 2.5rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--tf-text-secondary);
      cursor: pointer;
      accent-color: var(--tf-primary);
    }

    /* The "Project manager" column heading already names the checkbox, so the word is only shown where the heading is hidden. */
    .team-row__manager-text {
      display: none;
    }

    .team-row__manager.is-disabled {
      cursor: not-allowed;
      opacity: 0.55;
    }

    /* Narrow: the heading row goes away because every field carries its own label. */
    @media (max-width: 575.98px) {
      .team-head {
        display: none;
      }

      .team-row {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }

      .team-cell--wide {
        grid-column: 1 / -1;
      }

      .team-cell::before {
        content: attr(data-label);
        display: block;
        margin-bottom: 0.25rem;
        font-size: 0.75rem;
        font-weight: 600;
        color: var(--tf-text-secondary);
      }

      .team-row__manager {
        align-self: end;
      }

      .team-row__manager-text {
        display: inline;
      }

      .team-row__remove {
        grid-column: 1 / -1;
        justify-self: end;
      }
    }
  `
})
export class ProjectTeamDialogComponent {
  private readonly projectService = inject(ProjectService);
  private readonly positionService = inject(PositionService);
  private readonly toast = inject(ToastService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  /** Emits after the team was saved. */
  readonly saved = output<number>();

  readonly maxPercent = MAX_TOTAL_PERCENT;
  readonly projectId = signal<number | null>(null);
  readonly projectName = signal('');
  readonly afterCreate = signal(false);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly positions = signal<Position[]>([]);
  readonly rows = signal<TeamRow[]>([]);
  private nextKey = 1;

  readonly chosenUserIds = computed(() => this.rows().flatMap((row) => (row.userId === null ? [] : [row.userId])));
  readonly hasErrors = computed(() => this.rows().some((row) => this.rowError(row) !== null));
  /** A team with people on it needs at least one manager; an empty team needs none. */
  readonly managerError = computed(() =>
    this.rows().length > 0 && !this.rows().some((row) => row.isManager) ? 'Mark at least one person as the project manager.' : null
  );

  open(project: { id: number; name: string }, afterCreate = false): void {
    this.projectId.set(project.id);
    this.projectName.set(project.name);
    this.afterCreate.set(afterCreate);
    this.rows.set([]);
    this.serverError.set(null);
    this.loadError.set(null);
    this.submitted.set(false);
    this.saving.set(false);
    this.loading.set(true);

    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      dialog.showModal();
    }

    this.positionService.getAll().subscribe({
      next: (positions) => this.positions.set(positions),
      error: () => this.positions.set([])
    });
    this.projectService.getMembers(project.id).subscribe({
      next: (members) => {
        this.rows.set(members.map((member) => this.fromMember(member)));
        this.loading.set(false);
      },
      error: (err) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the team. Close this and try again.'));
        this.loading.set(false);
      }
    });
  }

  close(): void {
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      dialog.close();
    }
  }

  onCancelEvent(event: Event): void {
    event.preventDefault();
    this.close();
  }

  private fromMember(member: ProjectMember): TeamRow {
    return {
      key: this.nextKey++,
      userId: member.userId,
      fullName: member.fullName,
      positionId: member.positionId,
      percent: member.percent,
      capacity: null,
      isManager: member.isManager,
      canManage: member.isManagerRole
    };
  }

  add(): void {
    this.rows.update((rows) => [
      ...rows,
      {
        key: this.nextKey++,
        userId: null,
        fullName: '',
        positionId: null,
        percent: null,
        capacity: null,
        isManager: false,
        canManage: false
      }
    ]);
  }

  remove(key: number): void {
    this.rows.update((rows) => rows.filter((row) => row.key !== key));
  }

  update(key: number, change: Partial<Omit<TeamRow, 'key'>>): void {
    this.rows.update((rows) => rows.map((row) => (row.key === key ? { ...row, ...change } : row)));
  }

  /** A freshly picked person: their remaining share is the most they can take here. */
  pick(key: number, user: User): void {
    const row = this.rows().find((candidate) => candidate.key === key);
    this.update(key, {
      userId: user.id,
      fullName: user.fullName,
      canManage: user.roleId === SYSTEM_ROLE_IDS.Manager,
      isManager: user.roleId === SYSTEM_ROLE_IDS.Manager && (row?.userId === user.id ? row.isManager : false),
      capacity: user.remainingPercent,
      percent: row?.percent ?? (user.remainingPercent > 0 ? Math.min(user.remainingPercent, MAX_TOTAL_PERCENT) : null)
    });
  }

  setPercent(key: number, raw: string): void {
    this.update(key, { percent: raw.trim() === '' ? null : Number(raw) });
  }

  onPositionCreated(position: Position): void {
    this.positions.update((positions) => (positions.some((existing) => existing.id === position.id) ? positions : [...positions, position]));
  }

  /** Why one row cannot be saved, or null. Only complete-looking input is judged until the user tries to save. */
  rowError(row: TeamRow): string | null {
    if (row.percent !== null && (!Number.isInteger(row.percent) || row.percent < 1 || row.percent > MAX_TOTAL_PERCENT)) {
      return `Use a whole percentage from 1 to ${MAX_TOTAL_PERCENT}.`;
    }
    if (row.percent !== null && row.capacity !== null && row.percent > row.capacity) {
      return row.capacity <= 0
        ? `${row.fullName} is fully allocated on other projects.`
        : `${row.fullName} has only ${row.capacity}% remaining.`;
    }
    return null;
  }

  private incomplete(): boolean {
    return this.rows().some((row) => row.userId === null || row.positionId === null || row.percent === null);
  }

  save(): void {
    this.submitted.set(true);
    this.serverError.set(null);
    const projectId = this.projectId();
    if (projectId === null || this.saving() || this.hasErrors() || this.managerError() !== null) {
      return;
    }
    if (this.incomplete()) {
      this.serverError.set('Give every row an employee, a position and a percentage, or remove the row.');
      return;
    }

    this.saving.set(true);
    const members = this.rows().map((row) => ({
      userId: row.userId!,
      positionId: row.positionId!,
      percent: row.percent!,
      isManager: row.isManager
    }));
    this.projectService.replaceMembers(projectId, members).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(members.length === 0 ? 'Team cleared' : 'Team saved');
        this.close();
        this.saved.emit(projectId);
      },
      error: (err) => {
        this.saving.set(false);
        // The server rejects the whole request when anyone would pass 100%, and says who.
        this.serverError.set(apiErrorMessage(err, 'Could not save the team.'));
      }
    });
  }
}
