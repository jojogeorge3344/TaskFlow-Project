import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
import { EMAIL_SUFFIX, localPartOf, localPartValidator, toEmail } from '../../core/email-domain';
import { IconComponent } from '../../core/layout/icon.component';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { EmailLocalPartDirective } from '../../shared/ui/email-local-part.directive';
import { ToastService } from '../../shared/ui/toast.service';

const MIN_PASSWORD_LENGTH = 6;
/** Mirrors the server rule: login rejects passwords longer than this, so the change form must too. */
const MAX_PASSWORD_LENGTH = 128;

/** Mirrors the server rule for matching password confirmation. */
function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as { newPassword: string; confirmPassword: string };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

type ProfileField = 'fullName' | 'email';
type PasswordField = 'currentPassword' | 'newPassword' | 'confirmPassword';

/** User Profile (Screen 19). Every signed-in user views and maintains their own account here.
 * Role, project, username, employee code and status are read-only by design. */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent, EmailLocalPartDirective],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly emailSuffix = EMAIL_SUFFIX;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly profile = signal<User | null>(null);

  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedAt = signal<Date | null>(null);

  readonly changingPassword = signal(false);
  readonly passwordError = signal<string | null>(null);

  readonly readonlyFields = computed(() => {
    const user = this.profile();
    return user
      ? [
          { label: 'Username', value: user.username },
          { label: 'Employee code', value: user.employeeCode || '—' },
          { label: 'Role', value: user.role },
          { label: 'Project', value: user.projectName || '—' },
          { label: 'Manager', value: user.managerName || '—' },
          ...(user.role === 'Manager'
            ? [
                { label: 'Managed projects', value: (user.managerProjects ?? []).map((d) => d.name).join(', ') || '—' },
                { label: 'Team', value: (user.reports ?? []).map((r) => r.fullName).join(', ') || '—' }
              ]
            : []),
          { label: 'Account status', value: user.isActive ? 'Active' : 'Inactive' }
        ]
      : [];
  });

  readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    email: new FormControl('', {
      nonNullable: true,
      // Only the part before the fixed domain; the suffix is shown beside the input and added on save.
      validators: [Validators.required, localPartValidator]
    })
  });

  readonly passwordForm = new FormGroup(
    {
      currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH), Validators.maxLength(MAX_PASSWORD_LENGTH)]
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
    },
    { validators: passwordsMatch }
  );

  constructor() {
    // A later edit makes the "Saved" confirmation stale.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.savedAt.set(null));
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.profileService.get().subscribe({
      next: (user) => {
        this.profile.set(user);
        this.form.reset({ fullName: user.fullName, email: localPartOf(user.email) });
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load your profile.'));
        this.loading.set(false);
      }
    });
  }

  fieldError(name: ProfileField): string | null {
    const control = this.form.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (control.getError('required')) {
      return `${name === 'fullName' ? 'Full name' : 'Email'} is required.`;
    }
    if (control.getError('localPart')) {
      return `Use letters, digits, dot, underscore or hyphen before ${EMAIL_SUFFIX}.`;
    }
    if (control.getError('maxlength')) {
      return 'This field is too long.';
    }
    return null;
  }

  passwordFieldError(name: PasswordField): string | null {
    const control = this.passwordForm.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (control.getError('required')) {
      return 'Required.';
    }
    if (name === 'newPassword' && control.getError('minlength')) {
      return `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'newPassword' && control.getError('maxlength')) {
      return `Password must be at most ${MAX_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'confirmPassword' && this.passwordForm.hasError('mismatch')) {
      return 'Passwords do not match.';
    }
    return null;
  }

  save(): void {
    this.saveError.set(null);
    this.savedAt.set(null);
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.profileService
      .update({ fullName: value.fullName.trim(), email: toEmail(value.email) })
      .subscribe({
        next: (user) => {
          this.saving.set(false);
          this.profile.set(user);
          this.form.reset({ fullName: user.fullName, email: localPartOf(user.email) });
          this.savedAt.set(new Date());
          this.authService.applyProfile({
            id: user.id,
            fullName: user.fullName,
            username: user.username,
            email: user.email,
            role: user.role,
            employeeCode: user.employeeCode ?? null
          });
          this.toast.success('Profile updated');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.applyServerErrors(err, this.form);
          this.saveError.set(apiErrorMessage(err, 'Could not update your profile.'));
        }
      });
  }

  changePassword(): void {
    this.passwordError.set(null);
    if (this.passwordForm.invalid || this.changingPassword()) {
      return;
    }

    const value = this.passwordForm.getRawValue();
    this.changingPassword.set(true);
    this.profileService
      .changePassword({ currentPassword: value.currentPassword, newPassword: value.newPassword })
      .subscribe({
        next: () => {
          // The API ended every session, including this one; sign in again with the new password.
          this.authService.clearSession();
          void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
        },
        error: (err: unknown) => {
          this.changingPassword.set(false);
          this.applyServerErrors(err, this.passwordForm);
          this.passwordError.set(apiErrorMessage(err, 'Could not change your password.'));
        }
      });
  }

  /** Maps a validation ProblemDetails `errors` map (PascalCase keys) onto the matching controls. */
  private applyServerErrors(err: unknown, form: FormGroup): void {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (!errors) {
      return;
    }
    for (const [key, messages] of Object.entries(errors)) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof typeof form.controls;
      const control = form.controls[name];
      if (control && messages?.length) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
