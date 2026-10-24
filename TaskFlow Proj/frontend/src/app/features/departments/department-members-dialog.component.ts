import { Component, ElementRef, computed, inject, output, signal, viewChild } from '@angular/core';
import { Department, DepartmentMembers, MemberRef, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { UserService } from '../../core/services/user.service';
import { IconComponent } from '../../core/layout/icon.component';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

export type MemberSort = 'name-asc' | 'name-desc' | 'selected' | 'role';

interface MemberOption {
  id: number;
  fullName: string;
  username: string;
  /** Whether the person is saved as a manager or as an employee of the department. */
  kind: 'manager' | 'employee';
  /** A current member who is no longer an active manager/employee; still listed so they are not silently dropped. */
  inactive: boolean;
}

/**
 * Choose a department's members from one searchable, sortable, multi-select list on the native <dialog>. Each person is
 * saved as a manager or an employee according to their role. Saving replaces the membership in one call, so every
 * checked id is sent. Inactive current members stay listed and checked until someone unchecks them.
 */
@Component({
  selector: 'app-department-members-dialog',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './department-members-dialog.component.html',
  styleUrl: './department-members-dialog.component.css'
})
export class DepartmentMembersDialogComponent {
  private readonly departmentService = inject(DepartmentService);
  private readonly userService = inject(UserService);
  private readonly toast = inject(ToastService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly saved = output<DepartmentMembers>();

  readonly department = signal<Department | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly loadError = signal<string | null>(null);

  readonly activeManagers = signal<User[]>([]);
  readonly activeEmployees = signal<User[]>([]);
  readonly members = signal<DepartmentMembers | null>(null);
  readonly selectedIds = signal<number[]>([]);
  readonly query = signal('');
  readonly sort = signal<MemberSort>('name-asc');

  private readonly allOptions = computed<MemberOption[]>(() => [
    ...this.optionsFor(this.activeManagers(), this.members()?.managers ?? [], 'manager'),
    ...this.optionsFor(this.activeEmployees(), this.members()?.employees ?? [], 'employee')
  ]);

  /** The people shown: filtered by the search text, then ordered by the chosen sort. */
  readonly visibleOptions = computed(() => {
    const term = this.query().trim().toLowerCase();
    const selected = new Set(this.selectedIds());
    const byName = (a: MemberOption, b: MemberOption) => a.fullName.localeCompare(b.fullName, undefined, { sensitivity: 'base' });
    const options = this.allOptions().filter(
      (option) => !term || option.fullName.toLowerCase().includes(term) || option.username.toLowerCase().includes(term)
    );
    switch (this.sort()) {
      case 'name-desc':
        return options.sort((a, b) => byName(b, a));
      case 'selected':
        return options.sort((a, b) => Number(selected.has(b.id)) - Number(selected.has(a.id)) || byName(a, b));
      case 'role':
        return options.sort((a, b) => a.kind.localeCompare(b.kind) || byName(a, b));
      default:
        return options.sort(byName);
    }
  });
  readonly selectedCount = computed(() => this.selectedIds().length);
  readonly allVisibleSelected = computed(() => {
    const visible = this.visibleOptions();
    const selected = new Set(this.selectedIds());
    return visible.length > 0 && visible.every((option) => selected.has(option.id));
  });

  open(department: Department): void {
    this.department.set(department);
    this.loading.set(true);
    this.saving.set(false);
    this.serverError.set(null);
    this.loadError.set(null);
    this.members.set(null);
    this.activeManagers.set([]);
    this.activeEmployees.set([]);
    this.selectedIds.set([]);
    this.query.set('');
    this.sort.set('name-asc');

    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      dialog.showModal();
    }
    this.load(department.id);
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

  isSelected(id: number): boolean {
    return this.selectedIds().includes(id);
  }

  toggle(id: number, checked: boolean): void {
    this.selectedIds.update((ids) => (checked ? [...ids.filter((selected) => selected !== id), id] : ids.filter((selected) => selected !== id)));
  }

  /** Selects everyone currently shown, or clears them when they are all selected already. Hidden people are untouched. */
  toggleAllVisible(): void {
    const visible = this.visibleOptions().map((option) => option.id);
    if (this.allVisibleSelected()) {
      this.selectedIds.update((ids) => ids.filter((id) => !visible.includes(id)));
    } else {
      this.selectedIds.update((ids) => [...ids, ...visible.filter((id) => !ids.includes(id))]);
    }
  }

  clearSelection(): void {
    this.selectedIds.set([]);
  }

  save(): void {
    const department = this.department();
    if (!department || this.saving() || this.loading() || this.loadError()) {
      return;
    }

    this.saving.set(true);
    this.serverError.set(null);
    const selected = new Set(this.selectedIds());
    const chosen = this.allOptions().filter((option) => selected.has(option.id));
    this.departmentService
      .replaceMembers(department.id, {
        managerIds: chosen.filter((option) => option.kind === 'manager').map((option) => option.id).sort((a, b) => a - b),
        employeeIds: chosen.filter((option) => option.kind === 'employee').map((option) => option.id).sort((a, b) => a - b)
      })
      .subscribe({
        next: (members) => {
          this.saving.set(false);
          this.toast.success(`${department.name} members updated`);
          this.close();
          this.saved.emit(members);
        },
        error: (err) => {
          this.saving.set(false);
          this.serverError.set(apiErrorMessage(err, 'Could not save the department members.'));
        }
      });
  }

  /** The active users to pick from, with any current member who is not among them appended and flagged inactive. */
  private optionsFor(active: User[], current: MemberRef[], kind: MemberOption['kind']): MemberOption[] {
    const options: MemberOption[] = active.map((user) => ({
      id: user.id,
      fullName: user.fullName,
      username: user.username,
      kind,
      inactive: false
    }));
    for (const member of current) {
      if (!options.some((option) => option.id === member.id)) {
        options.push({ id: member.id, fullName: member.fullName, username: member.username, kind, inactive: true });
      }
    }
    return options;
  }

  private load(id: number): void {
    this.departmentService.getMembers(id).subscribe({
      next: (members) => {
        this.members.set(members);
        this.selectedIds.set([...members.managers, ...members.employees].map((member) => member.id));
        this.loading.set(false);
      },
      error: (err) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the department members.'));
        this.loading.set(false);
      }
    });

    this.userService.getAll({ role: 'Manager', isActive: true }).subscribe({
      next: (managers) => this.activeManagers.set(managers),
      error: () => this.activeManagers.set([])
    });
    this.userService.getAll({ role: 'Employee', isActive: true }).subscribe({
      next: (employees) => this.activeEmployees.set(employees),
      error: () => this.activeEmployees.set([])
    });
  }
}
