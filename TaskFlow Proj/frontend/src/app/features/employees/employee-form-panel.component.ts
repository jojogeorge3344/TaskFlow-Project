import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
import { EMAIL_SUFFIX, localPartOf, localPartValidator, toEmail } from '../../core/email-domain';
import { Permissions } from '../../core/auth/permissions';
import {
  AccessRole,
  CreateUserRequest,
  DepartmentWithCount,
  MAX_TOTAL_PERCENT,
  Position,
  ProjectAllocationInput,
  ProjectWithCount,
  SYSTEM_ROLE_IDS,
  UpdateUserRequest,
  User
} from '../../core/models';
import { IconComponent } from '../../core/layout/icon.component';
import { PositionService } from '../../core/services/position.service';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { PositionComboboxComponent } from '../../shared/position-combobox/position-combobox.component';
import { EmailLocalPartDirective } from '../../shared/ui/email-local-part.directive';
import { initials } from '../../shared/ui/initials';
import { ToastService } from '../../shared/ui/toast.service';
import { AllocationRow } from './employee-form-panel.model';

export const MIN_PASSWORD_LENGTH = 6;

/** Required, ignoring surrounding whitespace. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Add/edit employee form shown inside the side panel. Saves through the API and reports the result. */
@Component({
  selector: 'app-employee-form-panel',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, IconComponent, EmailLocalPartDirective, PositionComboboxComponent],
  templateUrl: './employee-form-panel.component.html'
})
export class EmployeeFormPanelComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly roleService = inject(RoleService);
  private readonly positionService = inject(PositionService);
  private readonly destroyRef = inject(DestroyRef);

  /** The employee to edit, or null to add a new one. */
  readonly employee = input<User | null>(null);
  readonly projects = input<ProjectWithCount[]>([]);
  readonly departments = input<DepartmentWithCount[]>([]);
  readonly saved = output<User>();
  readonly cancelled = output<void>();

  /** Choosing a role needs the employees.role.assign permission. Others keep the current role. */
  readonly canChangeRole = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesRoleAssign);
  });
  readonly selectedRoleId = signal<number>(SYSTEM_ROLE_IDS.Employee);
  /** The projects a manager manages come from the manager flag on each project's team, so they are shown but never edited. */
  readonly managedProjects = computed(() => (this.employee()?.roleId === SYSTEM_ROLE_IDS.Manager ? (this.employee()?.managerProjects ?? []) : []));
  /** The user's managers are the managers of their departments and projects, so they are shown here but never edited. */
  readonly derivedManagers = computed(() => this.employee()?.managers ?? []);

  readonly positions = signal<Position[]>([]);
  readonly rows = signal<AllocationRow[]>([]);
  readonly departmentIds = signal<number[]>([]);
  readonly maxPercent = MAX_TOTAL_PERCENT;
  private nextRowKey = 1;

  /** Sum of the valid percentages typed so far. */
  readonly totalPercent = computed(() =>
    this.rows().reduce((sum, row) => sum + (this.isWholePercent(row.percent) ? row.percent : 0), 0)
  );
  readonly remainingPercent = computed(() => MAX_TOTAL_PERCENT - this.totalPercent());
  readonly overAllocated = computed(() => this.totalPercent() > MAX_TOTAL_PERCENT);
  /** Why the project rows cannot be saved yet, or null when they can. */
  readonly allocationError = computed<string | null>(() => {
    // Projects are only edited when adding; an existing person's projects are managed on the Project screens.
    if (!this.isCreate()) {
      return null;
    }
    const rows = this.rows();
    if (rows.some((row) => row.projectId === null || row.positionId === null || !this.isWholePercent(row.percent))) {
      return `Give every project row a project, a position and a whole percentage from 1 to ${MAX_TOTAL_PERCENT}.`;
    }
    const projectIds = rows.map((row) => row.projectId);
    if (new Set(projectIds).size !== projectIds.length) {
      return 'A project can only appear once.';
    }
    if (this.overAllocated()) {
      return `Allocations total ${this.totalPercent()}%. A person can be allocated at most ${MAX_TOTAL_PERCENT}%.`;
    }
    return null;
  });
  /** One segment of the capacity bar per project row; an incomplete percentage draws nothing. */
  readonly allocationSegments = computed(() =>
    this.rows().map((row, index) => ({
      key: row.key,
      width: this.isWholePercent(row.percent) ? Math.min(row.percent, MAX_TOTAL_PERCENT) : 0,
      className: `tf-alloc-${index % 5}`
    }))
  );
  readonly roles = signal<AccessRole[]>([]);
  readonly roleLoadFailed = signal(false);
  readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  readonly emailSuffix = EMAIL_SUFFIX;
  readonly isCreate = computed(() => this.employee() === null);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly showPassword = signal(false);
  private readonly typedPassword = signal('');
  readonly strengthSteps = [0, 1, 2, 3];
  /** 0 to 4: length of 8 or more, an uppercase letter, a digit and a symbol. Display only; it never affects validation. */
  readonly passwordStrength = computed(() => {
    const value = this.typedPassword();
    return [value.length >= 8, /[A-Z]/.test(value), /\d/.test(value), /[^A-Za-z0-9]/.test(value)].filter(Boolean).length;
  });
  readonly headerInitials = computed(() => initials(this.employee()?.fullName));
  /** The role shown in the header: the one picked in the form when it can be changed, otherwise the saved role. */
  readonly headerRoleName = computed(() =>
    this.canChangeRole()
      ? (this.roles().find((role) => role.id === this.selectedRoleId())?.name ?? this.currentRoleName())
      : this.currentRoleName()
  );
  readonly serverError = signal<string | null>(null);

  readonly form = new FormGroup({
    employeeCode: new FormControl('', { nonNullable: true }),
    fullName: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    username: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    // Holds only the part before the fixed domain; the suffix is shown beside the input and added on save.
    email: new FormControl('', { nonNullable: true, validators: [requiredText, localPartValidator] }),
    roleId: new FormControl<number>(SYSTEM_ROLE_IDS.Employee, { nonNullable: true }),
    password: new FormControl('', { nonNullable: true }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  constructor() {
    this.form.controls.roleId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((roleId) => this.selectedRoleId.set(roleId));
    this.form.controls.password.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => this.typedPassword.set(value));
  }

  ngOnInit(): void {
    this.loadRoles();
    this.loadPositions();
    const employee = this.employee();
    if (employee) {
      this.form.patchValue({
        employeeCode: employee.employeeCode ?? '',
        fullName: employee.fullName,
        username: employee.username,
        email: localPartOf(employee.email),
        roleId: employee.roleId
      });
      this.rows.set(
        employee.projectAllocations.map((allocation) => ({
          key: this.nextRowKey++,
          projectId: allocation.projectId,
          positionId: allocation.positionId,
          percent: allocation.percent
        }))
      );
      this.departmentIds.set(employee.departments.map((department) => department.id));
      this.selectedRoleId.set(employee.roleId);
      // Initial status and the password only exist when adding.
      this.form.controls.isActive.disable();
    } else {
      // Mirrors the API's CreateUserRequestValidator (required, at least 6 characters).
      this.form.controls.password.addValidators([requiredText, Validators.minLength(MIN_PASSWORD_LENGTH)]);
    }
  }

  initialsOf(name: string): string {
    return initials(name);
  }

  /** The name shown when the role cannot be changed here. */
  currentRoleName(): string {
    return this.employee()?.role ?? 'Employee';
  }

  private loadRoles(): void {
    if (!this.canChangeRole()) {
      return;
    }

    this.roleService.assignable().subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => {
        // Fall back to the system roles so the form stays usable.
        this.roleLoadFailed.set(true);
        this.roles.set([
          { id: SYSTEM_ROLE_IDS.Employee, name: 'Employee', isSystem: true, userCount: 0, permissionKeys: [] },
          { id: SYSTEM_ROLE_IDS.Manager, name: 'Manager', isSystem: true, userCount: 0, permissionKeys: [] },
          { id: SYSTEM_ROLE_IDS.Administrator, name: 'Administrator', isSystem: true, userCount: 0, permissionKeys: [] }
        ]);
      }
    });
  }

  private loadPositions(): void {
    this.positionService.getAll().subscribe({
      next: (positions) => this.positions.set(positions),
      error: () => this.positions.set([])
    });
  }

  private isWholePercent(percent: number | null): percent is number {
    return percent !== null && Number.isInteger(percent) && percent >= 1 && percent <= MAX_TOTAL_PERCENT;
  }

  /** The position a new row starts with: Member when it exists, otherwise nothing until one is picked. */
  private defaultPositionId(): number | null {
    return this.positions().find((position) => position.name.toLowerCase() === 'member')?.id ?? null;
  }

  addRow(): void {
    const used = new Set(this.rows().map((row) => row.projectId));
    const free = this.projects().find((project) => !used.has(project.id));
    this.rows.update((rows) => [
      ...rows,
      {
        key: this.nextRowKey++,
        projectId: free?.id ?? null,
        positionId: this.defaultPositionId(),
        percent: Math.max(1, this.remainingPercent())
      }
    ]);
  }

  removeRow(key: number): void {
    this.rows.update((rows) => rows.filter((row) => row.key !== key));
  }

  updateRow(key: number, change: Partial<Omit<AllocationRow, 'key'>>): void {
    this.rows.update((rows) => rows.map((row) => (row.key === key ? { ...row, ...change } : row)));
  }

  setRowPercent(key: number, raw: string): void {
    this.updateRow(key, { percent: raw.trim() === '' ? null : Number(raw) });
  }

  /** Whether a project is already used by another row, so the select can hide it. */
  isProjectTaken(projectId: number, key: number): boolean {
    return this.rows().some((row) => row.key !== key && row.projectId === projectId);
  }

  onPositionCreated(position: Position): void {
    this.positions.update((positions) => (positions.some((existing) => existing.id === position.id) ? positions : [...positions, position]));
  }

  isDepartmentChosen(departmentId: number): boolean {
    return this.departmentIds().includes(departmentId);
  }

  toggleDepartment(departmentId: number, chosen: boolean): void {
    this.departmentIds.update((ids) =>
      chosen ? [...ids.filter((id) => id !== departmentId), departmentId] : ids.filter((id) => id !== departmentId)
    );
  }

  showError(name: 'fullName' | 'username' | 'email' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);
    if (this.form.invalid || this.allocationError() || this.saving()) {
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const common = {
      employeeCode: value.employeeCode.trim() || null,
      fullName: value.fullName.trim(),
      username: value.username.trim(),
      email: toEmail(value.email),
      roleId: value.roleId,
      projectAllocations: this.rows().map(
        (row): ProjectAllocationInput => ({ projectId: row.projectId!, positionId: row.positionId!, percent: row.percent! })
      ),
      departmentIds: this.departmentIds()
    };

    const employee = this.employee();
    const newPassword = employee && value.password ? value.password : null;
    const request$ = employee
      ? this.userService.update(employee.id, { ...common, password: newPassword } satisfies UpdateUserRequest)
      : this.userService.create({ ...common, password: value.password, isActive: value.isActive } satisfies CreateUserRequest);

    request$.subscribe({
      next: (user) => {
        this.saving.set(false);

        // The API ends every session of a user whose username, password or role changed.
        const credentialsChanged =
          employee !== null &&
          (user.username !== employee.username || newPassword !== null || user.roleId !== employee.roleId);

        if (credentialsChanged && this.authService.currentUser()?.id === user.id) {
          // The signed-in admin changed their own credentials: sign out now and ask for the new ones.
          this.authService.clearSession();
          void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
          return;
        }

        if (!employee) {
          this.toast.success(`${user.fullName} added`);
        } else {
          this.toast.success(
            credentialsChanged ? `${user.fullName} updated. They must sign in again.` : `${user.fullName} updated`
          );
        }
        this.saved.emit(user);
      },
      error: (err) => {
        this.saving.set(false);
        this.serverError.set(apiErrorMessage(err, employee ? 'Could not update the employee.' : 'Could not add the employee.'));
      }
    });
  }
}
