import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, filter, map, of, switchMap } from 'rxjs';
import { IconComponent } from '../../core/layout/icon.component';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { DepartmentWithCount, ProjectWithCount, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { RoleService } from '../../core/services/role.service';
import { ProjectService } from '../../core/services/project.service';
import { UserService } from '../../core/services/user.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { initials } from '../../shared/ui/initials';
import { ToastService } from '../../shared/ui/toast.service';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';
import { EmployeeFormPanelComponent } from './employee-form-panel.component';
import { roleBadgeClass } from './role-badge';

export const SEARCH_DEBOUNCE_MS = 300;

type StatusFilter = '' | 'active' | 'inactive';

type Panel = { mode: 'create' } | { mode: 'edit'; employee: User } | { mode: 'details'; employee: User };

@Component({
  selector: 'app-employee-list',
  standalone: true,
  imports: [IconComponent, DrawerComponent, ConfirmDialogComponent, EmployeeFormPanelComponent, EmployeeDetailsPanelComponent],
  templateUrl: './employee-list.component.html',
  styleUrl: './employee-list.component.css'
})
export class EmployeeListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly roleService = inject(RoleService);
  private readonly projectService = inject(ProjectService);
  private readonly departmentService = inject(DepartmentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  private readonly searchInput$ = new Subject<string>();
  private readonly reload$ = new Subject<void>();

  /** Filter choices: the system roles, plus custom roles for those allowed to list roles. */
  readonly roles = signal<string[]>(['Employee', 'Manager', 'Administrator']);
  readonly roleBadgeClass = roleBadgeClass;
  readonly initials = initials;

  readonly employees = signal<User[]>([]);
  readonly projects = signal<ProjectWithCount[]>([]);
  readonly departments = signal<DepartmentWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyId = signal<number | null>(null);

  readonly canCreate = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesCreate);
  });

  readonly canEdit = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesEdit);
  });

  readonly canChangeStatus = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesStatus);
  });

  /** Deleting an employee needs its own permission. */
  readonly canDelete = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesDelete);
  });

  /** What is typed in the search box; `search` is the debounced value that is applied. */
  readonly searchText = signal('');
  readonly search = signal('');
  readonly projectId = signal<number | null>(null);
  readonly role = signal<string>('');
  readonly status = signal<StatusFilter>('');

  readonly hasFilters = computed(
    () => this.search() !== '' || this.projectId() !== null || this.role() !== '' || this.status() !== ''
  );

  readonly countLabel = computed(() => {
    const count = this.employees().length;
    return count === 1 ? '1 employee' : `${count} employees`;
  });

  readonly panel = signal<Panel | null>(null);
  readonly panelTitle = computed(() => {
    switch (this.panel()?.mode) {
      case 'create':
        return 'Add employee';
      case 'edit':
        return 'Edit employee';
      case 'details':
        return 'Employee details';
      default:
        return '';
    }
  });

  constructor() {
    const projectId = Number(this.route.snapshot.queryParamMap.get('projectId'));
    if (Number.isInteger(projectId) && projectId > 0) {
      this.projectId.set(projectId);
    }

    this.searchInput$
      .pipe(
        debounceTime(SEARCH_DEBOUNCE_MS),
        map((term) => term.trim()),
        // Skip when the applied search is unchanged (e.g. only whitespace was added).
        filter((term) => term !== this.search()),
        takeUntilDestroyed()
      )
      .subscribe((term) => {
        this.search.set(term);
        this.load();
      });

    // switchMap cancels the previous request so only the latest filters win.
    this.reload$
      .pipe(
        switchMap(() => this.fetchEmployees()),
        takeUntilDestroyed()
      )
      .subscribe((employees) => {
        if (employees === null) {
          this.error.set('Could not load employees. Check your connection and try again.');
        } else {
          this.employees.set(employees);
        }
        this.loading.set(false);
      });
  }

  ngOnInit(): void {
    this.projectService.getAll().subscribe({
      next: (projects) => this.projects.set(projects)
    });
    this.departmentService.getAll().subscribe({
      next: (departments) => this.departments.set(departments)
    });
    if (this.authService.hasPermission(Permissions.RolesManage) || this.authService.hasPermission(Permissions.EmployeesRoleAssign)) {
      // Custom roles join the filter; the standard three stay if the list cannot be loaded.
      this.roleService.assignable().subscribe({
        next: (roles) => this.roles.set(roles.map((role) => role.name))
      });
    }
    this.load();
  }

  load(): void {
    this.reload$.next();
  }

  onSearch(value: string): void {
    this.searchText.set(value);
    this.searchInput$.next(value);
  }

  setProject(value: string): void {
    this.projectId.set(value ? Number(value) : null);
    this.syncUrl();
    this.load();
  }

  setRole(value: string): void {
    this.role.set(value);
    this.load();
  }

  setStatus(value: string): void {
    this.status.set(value as StatusFilter);
    this.load();
  }

  resetFilters(): void {
    this.searchText.set('');
    this.search.set('');
    this.projectId.set(null);
    this.role.set('');
    this.status.set('');
    this.syncUrl();
    this.load();
  }

  openCreate(): void {
    this.panel.set({ mode: 'create' });
  }

  openDetails(employee: User): void {
    this.panel.set({ mode: 'details', employee });
  }

  openEdit(employee: User): void {
    this.panel.set({ mode: 'edit', employee });
  }

  closePanel(): void {
    this.panel.set(null);
  }

  onSaved(): void {
    this.closePanel();
    this.load();
  }

  async toggleStatus(employee: User): Promise<void> {
    if (employee.isActive) {
      const confirmed = await this.confirmDialog().open({
        title: 'Deactivate employee?',
        message: `${employee.fullName} will not be able to sign in until they are activated again.`,
        confirmLabel: 'Deactivate',
        variant: 'danger'
      });
      if (!confirmed) {
        return;
      }
    }

    const activate = !employee.isActive;
    this.busyId.set(employee.id);
    this.userService.updateStatus(employee.id, { isActive: activate }).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success(`${employee.fullName} ${activate ? 'activated' : 'deactivated'}`);
        this.load();
      },
      error: (err) => {
        this.busyId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not update the employee status.'));
      }
    });
  }

  /** Whether the signed-in user may remove another user's MFA. */
  canResetMfa(): boolean {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesMfaReset);
  }

  async resetMfa(employee: User): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Reset two-step verification?',
      message: `${employee.fullName} will be signed out everywhere and can sign in with just their password. They can set up an authenticator app again from Profile.`,
      confirmLabel: 'Reset MFA',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.busyId.set(employee.id);
    this.userService.resetMfa(employee.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success(`MFA reset for ${employee.fullName}`);
        this.panel.set({ mode: 'details', employee: { ...employee, mfaEnabled: false, backupCodesRemaining: 0 } });
        this.load();
      },
      error: (err) => {
        this.busyId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not reset MFA.'));
      }
    });
  }

  /** Nobody deletes their own account, so the action is not offered on the signed-in user's row. */
  canDeleteEmployee(employee: User): boolean {
    return this.canDelete() && this.authService.currentUser()?.id !== employee.id;
  }

  async deleteEmployee(employee: User): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Delete employee?',
      message: `${employee.fullName} will no longer appear in the system or be able to sign in. Their tasks, comments and mail are kept.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.busyId.set(employee.id);
    this.userService.delete(employee.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success(`${employee.fullName} deleted`);
        this.load();
      },
      error: (err) => {
        this.busyId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not delete the employee.'));
      }
    });
  }

  private fetchEmployees(): Observable<User[] | null> {
    this.loading.set(true);
    this.error.set(null);

    const params: { search?: string; projectId?: number; role?: string; isActive?: boolean } = {};
    if (this.search()) params.search = this.search();
    if (this.projectId() !== null) params.projectId = this.projectId()!;
    if (this.role()) params.role = this.role();
    if (this.status()) params.isActive = this.status() === 'active';

    return this.userService.getAll(params).pipe(catchError(() => of(null)));
  }

  private syncUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { projectId: this.projectId() },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }
}
