import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { DepartmentWithCount } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { IconComponent } from '../../core/layout/icon.component';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentFormDialogComponent } from './department-form-dialog.component';
import { DepartmentMembersDialogComponent } from './department-members-dialog.component';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [IconComponent, ConfirmDialogComponent, DepartmentFormDialogComponent, DepartmentMembersDialogComponent],
  templateUrl: './department-list.component.html',
  styleUrl: './department-list.component.css'
})
export class DepartmentListComponent implements OnInit {
  private readonly departmentService = inject(DepartmentService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);

  readonly canCreate = computed(() => this.can(Permissions.DepartmentsCreate));
  readonly canEdit = computed(() => this.can(Permissions.DepartmentsEdit));
  readonly canDelete = computed(() => this.can(Permissions.DepartmentsDelete));
  readonly canMembers = computed(() => this.can(Permissions.DepartmentsMembers));

  private can(key: string): boolean {
    this.authService.currentUser();
    return this.authService.hasPermission(key);
  }
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);
  private readonly formDialog = viewChild.required(DepartmentFormDialogComponent);
  private readonly membersDialog = viewChild.required(DepartmentMembersDialogComponent);

  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly query = signal('');
  readonly deletingId = signal<number | null>(null);

  /** Client-side search over the name. */
  readonly filtered = computed(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return this.departments();
    }
    return this.departments().filter((department) => department.name.toLowerCase().includes(term));
  });

  readonly countLabel = computed(() => {
    const count = this.departments().length;
    return count === 1 ? '1 department' : `${count} departments`;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.departmentService.getAll().subscribe({
      next: (departments) => {
        this.departments.set(departments);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load departments. Check your connection and try again.');
        this.loading.set(false);
      }
    });
  }

  hasMembers(department: DepartmentWithCount): boolean {
    return department.managerCount > 0 || department.employeeCount > 0;
  }

  managerLabel(count: number): string {
    return count === 1 ? 'manager' : 'managers';
  }

  employeeLabel(count: number): string {
    return count === 1 ? 'employee' : 'employees';
  }

  openCreate(): void {
    this.formDialog().open(null);
  }

  openEdit(department: DepartmentWithCount): void {
    this.formDialog().open(department);
  }

  openMembers(department: DepartmentWithCount): void {
    this.membersDialog().open(department);
  }

  async delete(department: DepartmentWithCount): Promise<void> {
    if (this.hasMembers(department)) {
      return;
    }

    const confirmed = await this.confirmDialog().open({
      title: 'Delete department?',
      message: `"${department.name}" will be permanently deleted.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.deletingId.set(department.id);
    this.departmentService.remove(department.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.toast.success(`${department.name} deleted`);
        this.load();
      },
      error: (err) => {
        this.deletingId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not delete the department.'));
      }
    });
  }
}
