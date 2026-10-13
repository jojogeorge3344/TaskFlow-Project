import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { DepartmentWithCount } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentFormDialogComponent } from './department-form-dialog.component';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent, DepartmentFormDialogComponent],
  templateUrl: './department-list.component.html',
  styleUrl: './department-list.component.css'
})
export class DepartmentListComponent implements OnInit {
  private readonly departmentService = inject(DepartmentService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);
  private readonly formDialog = viewChild.required(DepartmentFormDialogComponent);

  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly query = signal('');
  readonly deletingId = signal<number | null>(null);

  /** Client-side search over name and description. */
  readonly filtered = computed(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return this.departments();
    }
    return this.departments().filter(
      (department) =>
        department.name.toLowerCase().includes(term) || (department.description ?? '').toLowerCase().includes(term)
    );
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

  employeeLabel(count: number): string {
    return count === 1 ? 'employee' : 'employees';
  }

  openCreate(): void {
    this.formDialog().open(null);
  }

  openEdit(department: DepartmentWithCount): void {
    this.formDialog().open(department);
  }

  async delete(department: DepartmentWithCount): Promise<void> {
    if (department.employeeCount > 0) {
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
    this.departmentService.delete(department.id).subscribe({
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
