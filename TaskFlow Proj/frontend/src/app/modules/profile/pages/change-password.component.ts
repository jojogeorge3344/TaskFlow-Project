import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../shared/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../../shared/auth/login-reasons';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { ProfileService } from '../services/profile.service';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { PasswordField } from './change-password.model';

const MIN_PASSWORD_LENGTH = 6;
/** Mirrors the server rule: login rejects passwords longer than this, so the change form must too. */
const MAX_PASSWORD_LENGTH = 128;

/** Mirrors the server rule for matching password confirmation. */
function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as { newPassword: string; confirmPassword: string };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

/** A rough 0-4 score so the user sees how strong a new password is. Only a hint: the API enforces the real rules. */
export function passwordStrength(password: string): { level: number; label: string } {
  if (!password) {
    return { level: 0, label: '' };
  }

  let points = 0;
  if (password.length >= MIN_PASSWORD_LENGTH) points++;
  if (password.length >= 10) points++;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) points++;
  if (/d/.test(password) && /[^A-Za-z0-9]/.test(password)) points++;

  const level = password.length < MIN_PASSWORD_LENGTH ? 1 : Math.max(1, Math.min(4, points));
  return { level, label: ['', 'Weak', 'Fair', 'Good', 'Strong'][level] };
}

/** Change password, opened from Profile. The form takes only the new password; clicking Change password asks for the
 * authenticator code in a popup, and the password is saved only if the server accepts that code. */
@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconComponent],
  templateUrl: './change-password.component.html',
  styleUrls: ['./profile-shared.css', './change-password.component.css']
})
export class ChangePasswordComponent {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('codeDialog');

  readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  readonly maxPasswordLength = MAX_PASSWORD_LENGTH;

  readonly showNew = signal(false);
  readonly showConfirm = signal(false);
  readonly changing = signal(false);
  readonly formError = signal<string | null>(null);
  readonly codeError = signal<string | null>(null);

  readonly passwordForm = new FormGroup(
    {
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH), Validators.maxLength(MAX_PASSWORD_LENGTH)]
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
    },
    { validators: passwordsMatch }
  );

  /** The authenticator code (or a backup code), asked for in the popup only. */
  readonly codeForm = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  private readonly newPasswordValue = signal('');
  readonly strength = computed(() => passwordStrength(this.newPasswordValue()));

  constructor() {
    this.passwordForm.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((value) => this.newPasswordValue.set(value));
  }

  toggle(field: 'new' | 'confirm'): void {
    (field === 'new' ? this.showNew : this.showConfirm).update((on) => !on);
  }

  passwordFieldError(name: PasswordField): string | null {
    const control = this.passwordForm.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    // A field only complains once the user has typed in it or left it (or tried to submit).
    if (!control.touched && !control.dirty) {
      return null;
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

  /** The Change password button: a valid form opens the code popup; nothing is sent yet. */
  submit(): void {
    this.formError.set(null);
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.codeError.set(null);
    this.codeForm.reset();
    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      dialog.showModal();
    }
  }

  cancelCode(): void {
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      dialog.close();
    }
    this.codeError.set(null);
    this.codeForm.reset();
  }

  onDialogCancel(event: Event): void {
    event.preventDefault();
    this.cancelCode();
  }

  /** The popup's button: sends the new password with the code. Saved only if the server accepts the code. */
  confirmCode(): void {
    this.codeError.set(null);
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    if (this.changing()) {
      return;
    }

    this.changing.set(true);
    this.profileService
      .changePassword({
        newPassword: this.passwordForm.controls.newPassword.value,
        mfaCode: this.codeForm.controls.code.value.trim()
      })
      .subscribe({
        next: () => {
          // The API ended every session, including this one; sign in again with the new password.
          this.authService.clearSession();
          void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
        },
        error: (err: unknown) => {
          this.changing.set(false);
          this.codeForm.controls.code.reset('');
          if (this.applyPasswordErrors(err)) {
            // The server rejected the password itself, not the code: back to the form to fix it.
            this.cancelCode();
            this.formError.set(apiErrorMessage(err, 'Could not change your password.'));
            return;
          }
          this.codeError.set(apiErrorMessage(err, 'That code did not work. Try the new code from your app.'));
        }
      });
  }

  /** Maps a validation ProblemDetails `errors` map onto the password fields; true when it touched any. */
  private applyPasswordErrors(err: unknown): boolean {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return false;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (!errors) {
      return false;
    }
    let applied = false;
    for (const [key, messages] of Object.entries(errors)) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as PasswordField;
      const control = this.passwordForm.controls[name];
      if (control && messages?.length) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
        applied = true;
      }
    }
    return applied;
  }
}
