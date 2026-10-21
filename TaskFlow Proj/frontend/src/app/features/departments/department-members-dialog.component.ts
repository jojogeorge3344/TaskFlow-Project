import { Component, ElementRef, computed, inject, output, signal, viewChild } from '@angular/core';
import { Department, DepartmentMembers, MemberRef, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { UserService } from '../../core/services/user.service';
import { IconComponent } from '../../core/layout/icon.component';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

interface MemberOption {
  id: number;
  fullName: string;
  username: string;
  /** A current member who is no longer an active manager/employee; still listed so they are not silently dropped. */
  inactive: boolean;
}

/**
 * Choose a department's managers and employees on the native <dialog>. Saving replaces the membership in one call,
 * so every checked id is sent. Inactive current members stay listed and checked until someone unchecks them.
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
  readonly selectedManagerIds = signal<number[]>([]);
  readonly selectedEmployeeIds = signal<number[]>([]);

  readonly managerOptions = computed(() => this.optionsFor(this.activeManagers(), this.members()?.managers ?? []));
  readonly employeeOptions = computed(() => this.optionsFor(this.activeEmployees(), this.members()?.employees ?? []));

  open(department: Department): void {
    this.department.set(department);
    this.loading.set(true);
    this.saving.set(false);
    this.serverError.set(null);
    this.loadError.set(null);
    this.members.set(null);
    this.activeManagers.set([]);
    this.activeEmployees.set([]);
    this.selectedManagerIds.set([]);
    this.selectedEmployeeIds.set([]);

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

  isManagerSelected(id: number): boolean {
    return this.selectedManagerIds().includes(id);
  }

  toggleManager(id: number, checked: boolean): void {
    this.selectedManagerIds.update((ids) => (checked ? [...ids, id] : ids.filter((selected) => selected !== id)));
  }

  isEmployeeSelected(id: number): boolean {
    return this.selectedEmployeeIds().includes(id);
  }

  toggleEmployee(id: number, checked: boolean): void {
    this.selectedEmployeeIds.update((ids) => (checked ? [...ids, id] : ids.filter((selected) => selected !== id)));
  }

  save(): void {
    const department = this.department();
    if (!department || this.saving() || this.loading() || this.loadError()) {
      return;
    }

    this.saving.set(true);
    this.serverError.set(null);
    this.departmentService
      .replaceMembers(department.id, {
        managerIds: [...this.selectedManagerIds()],
        employeeIds: [...this.selectedEmployeeIds()]
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
  private optionsFor(active: User[], current: MemberRef[]): MemberOption[] {
    const options: MemberOption[] = active.map((user) => ({
      id: user.id,
      fullName: user.fullName,
      username: user.username,
      inactive: false
    }));
    for (const member of current) {
      if (!options.some((option) => option.id === member.id)) {
        options.push({ id: member.id, fullName: member.fullName, username: member.username, inactive: true });
      }
    }
    return options;
  }

  private load(id: number): void {
    this.departmentService.getMembers(id).subscribe({
      next: (members) => {
        this.members.set(members);
        this.selectedManagerIds.set(members.managers.map((manager) => manager.id));
        this.selectedEmployeeIds.set(members.employees.map((employee) => employee.id));
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
