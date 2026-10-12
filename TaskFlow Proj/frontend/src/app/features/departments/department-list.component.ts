import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { DepartmentService } from '../../core/services/department.service';
import { CreateDepartmentRequest, DepartmentWithCount, UpdateDepartmentRequest } from '../../core/models';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './department-list.component.html',
  styleUrl: './department-list.component.css'
})
export class DepartmentListComponent implements OnInit {
  private readonly departmentService = inject(DepartmentService);
  private readonly authService = inject(AuthService);

  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isAdmin = signal(false);
  readonly showForm = signal(false);
  readonly editing = signal<number | null>(null);

  name = '';
  description = '';

  ngOnInit(): void {
    this.isAdmin.set(this.authService.currentUser()?.role === 'Administrator');
    this.loadDepartments();
  }

  loadDepartments(): void {
    this.loading.set(true);
    this.error.set(null);
    this.departmentService.getAll().subscribe({
      next: (departments) => {
        this.departments.set(departments);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load departments');
        this.loading.set(false);
      }
    });
  }

  add(): void {
    this.showForm.set(true);
    this.editing.set(null);
    this.name = '';
    this.description = '';
  }

  edit(dept: DepartmentWithCount): void {
    if (!this.isAdmin()) return;
    this.showForm.set(true);
    this.editing.set(dept.id);
    this.name = dept.name;
    this.description = dept.description || '';
  }

  cancel(): void {
    this.showForm.set(false);
    this.editing.set(null);
    this.name = '';
    this.description = '';
  }

  save(): void {
    if (!this.name.trim()) return;
    this.loading.set(true);
    const request: CreateDepartmentRequest | UpdateDepartmentRequest = {
      name: this.name.trim(),
      description: this.description.trim() || null
    };
    if (this.editing() === null) {
      this.departmentService.create(request as CreateDepartmentRequest).subscribe({
        next: () => {
          this.loadDepartments();
          this.cancel();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to create department');
          this.loading.set(false);
        }
      });
    } else {
      this.departmentService.update(this.editing()!, request as UpdateDepartmentRequest).subscribe({
        next: () => {
          this.loadDepartments();
          this.cancel();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to update department');
          this.loading.set(false);
        }
      });
    }
  }

  delete(dept: DepartmentWithCount): void {
    if (!this.isAdmin() || dept.employeeCount > 0) return;
    if (confirm('Delete department?')) {
      this.loading.set(true);
      this.departmentService.delete(dept.id).subscribe({
        next: () => {
          this.loadDepartments();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to delete department');
          this.loading.set(false);
        }
      });
    }
  }
}
