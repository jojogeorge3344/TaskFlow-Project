import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
import { CreateUserRequest, DepartmentWithCount, Role, UpdateUserRequest, User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

export const ROLES: Role[] = ['Employee', 'Manager', 'Administrator'];
export const MIN_PASSWORD_LENGTH = 6;

/** Required, ignoring surrounding whitespace. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Optional, but a value that is only whitespace is not a usable password. */
function notBlankIfPresent(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  return value !== '' && value.trim() === '' ? { blank: true } : null;
}

/** Add/edit employee form shown inside the side panel. Saves through the API and reports the result. */
@Component({
  selector: 'app-employee-form-panel',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './employee-form-panel.component.html'
})
export class EmployeeFormPanelComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  /** The employee to edit, or null to add a new one. */
  readonly employee = input<User | null>(null);
  readonly departments = input<DepartmentWithCount[]>([]);
  readonly saved = output<User>();
  readonly cancelled = output<void>();

  readonly roles = ROLES;
  readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  readonly isCreate = computed(() => this.employee() === null);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form = new FormGroup({
    employeeCode: new FormControl('', { nonNullable: true }),
    fullName: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    username: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    email: new FormControl('', { nonNullable: true, validators: [requiredText, Validators.email] }),
    role: new FormControl<Role>('Employee', { nonNullable: true }),
    departmentId: new FormControl<number | null>(null),
    password: new FormControl('', { nonNullable: true }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  ngOnInit(): void {
    const employee = this.employee();
    if (employee) {
      this.form.patchValue({
        employeeCode: employee.employeeCode ?? '',
        fullName: employee.fullName,
        username: employee.username,
        email: employee.email,
        role: employee.role,
        departmentId: employee.departmentId ?? null
      });
      // Initial status only exists when adding; a new password is optional when editing.
      this.form.controls.isActive.disable();
      this.form.controls.password.addValidators([notBlankIfPresent, Validators.minLength(MIN_PASSWORD_LENGTH)]);
    } else {
      // Mirrors the API's CreateUserRequestValidator (required, at least 6 characters).
      this.form.controls.password.addValidators([requiredText, Validators.minLength(MIN_PASSWORD_LENGTH)]);
    }
  }

  showError(name: 'fullName' | 'username' | 'email' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const common = {
      employeeCode: value.employeeCode.trim() || null,
      fullName: value.fullName.trim(),
      username: value.username.trim(),
      email: value.email.trim(),
      role: value.role,
      departmentId: value.departmentId
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
          (user.username !== employee.username || newPassword !== null || user.role !== employee.role);

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
