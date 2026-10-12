import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { UserService } from '../../core/services/user.service';
import { DepartmentService } from '../../core/services/department.service';
import { CreateUserRequest, DepartmentWithCount, Role, UpdateUserRequest, User } from '../../core/models';

@Component({
  selector: 'app-employee-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './employee-list.component.html',
  styleUrl: './employee-list.component.css'
})
export class EmployeeListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly departmentService = inject(DepartmentService);
  private readonly authService = inject(AuthService);

  readonly employees = signal<User[]>([]);
  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly isAdmin = signal(false);
  readonly showForm = signal(false);
  readonly editing = signal<number | null>(null);

  search = '';
  departmentId: number | null = null;
  role: Role | '' = '';
  isActive: boolean | '' = '';

  fullName = '';
  username = '';
  email = '';
  password = '';
  employeeRole: Role = 'Employee';
  employeeDepartmentId: number | null = null;
  employeeActive = true;

  ngOnInit(): void {
    this.isAdmin.set(this.authService.currentUser()?.role === 'Administrator');
    this.loadDepartments();
    this.loadEmployees();
  }

  loadDepartments(): void {
    this.departmentService.getAll().subscribe({
      next: (departments) => this.departments.set(departments)
    });
  }

  loadEmployees(): void {
    this.loading.set(true);
    this.error.set(null);
    const params: any = {};
    if (this.search.trim()) params.search = this.search.trim();
    if (this.departmentId) params.departmentId = this.departmentId;
    if (this.role) params.role = this.role;
    if (this.isActive !== '') params.isActive = this.isActive === true;
    this.userService.getAll(params).subscribe({
      next: (employees) => {
        this.employees.set(employees);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load employees');
        this.loading.set(false);
      }
    });
  }

  add(): void {
    this.showForm.set(true);
    this.editing.set(null);
    this.fullName = '';
    this.username = '';
    this.email = '';
    this.password = '';
    this.employeeRole = 'Employee';
    this.employeeDepartmentId = null;
    this.employeeActive = true;
  }

  edit(user: User): void {
    if (!this.isAdmin()) return;
    this.showForm.set(true);
    this.editing.set(user.id);
    this.fullName = user.fullName;
    this.username = user.username;
    this.email = user.email;
    this.password = '';
    this.employeeRole = user.role;
    this.employeeDepartmentId = user.departmentId || null;
    this.employeeActive = user.isActive;
  }

  cancel(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  save(): void {
    if (!this.fullName.trim() || !this.username.trim() || !this.email.trim()) return;
    if (this.editing() === null && !this.password.trim()) return;
    this.loading.set(true);
    if (this.editing() === null) {
      const request: CreateUserRequest = {
        fullName: this.fullName.trim(),
        username: this.username.trim(),
        email: this.email.trim(),
        password: this.password.trim(),
        role: this.employeeRole,
        isActive: this.employeeActive,
        departmentId: this.employeeDepartmentId
      };
      this.userService.create(request).subscribe({
        next: () => {
          this.loadEmployees();
          this.cancel();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to create employee');
          this.loading.set(false);
        }
      });
    } else {
      const request: UpdateUserRequest = {
        fullName: this.fullName.trim(),
        username: this.username.trim(),
        email: this.email.trim(),
        role: this.employeeRole,
        departmentId: this.employeeDepartmentId
      };
      this.userService.update(this.editing()!, request).subscribe({
        next: () => {
          this.loadEmployees();
          this.cancel();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Failed to update employee');
          this.loading.set(false);
        }
      });
    }
  }

  toggleStatus(user: User): void {
    if (!this.isAdmin()) return;
    this.loading.set(true);
    this.userService.updateStatus(user.id, { isActive: !user.isActive }).subscribe({
      next: () => this.loadEmployees(),
      error: () => {
        this.error.set('Failed to update status');
        this.loading.set(false);
      }
    });
  }

  applyFilters(): void {
    this.loadEmployees();
  }
}
