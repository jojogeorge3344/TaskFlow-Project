import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../../core/auth/login-reasons';
import { ProfileService } from '../../../core/services/profile.service';
import { apiErrorMessage } from '../../../shared/ui/api-error';

const MIN_PASSWORD_LENGTH = 6;
/** Mirrors the server rule: sign-in rejects passwords longer than this, so the change form must too. */
const MAX_PASSWORD_LENGTH = 128;

function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as {
    newPassword: string;
    confirmPassword: string;
  };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

type Field = 'newPassword' | 'confirmPassword';

/** The mandatory password reset screen: where a user lands after signing in with an administrator-issued
 * password. It has no cancel, close or back control and sits outside the app shell; the API refuses every
 * other route with "Password change required" until the password is updated, and the auth interceptor
 * sends the user back here. No current password is asked for - signing in just proved it (design D11) -
 * and the temporary password is never displayed. */
@Component({
  selector: 'app-force-password-change',
  imports: [ReactiveFormsModule],
  templateUrl: './force-password-change.component.html',
  styleUrl: '../login/login.component.css'
})
export class ForcePasswordChangeComponent {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = new FormGroup(
    {
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH), Validators.maxLength(MAX_PASSWORD_LENGTH)]
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
    },
    { validators: passwordsMatch }
  );

  fieldError(name: Field): string | null {
    const control = this.form.controls[name];
    // Typing is enough to show a problem (the "do not match" message appears while confirming); fields the
    // user has not touched stay quiet until they try to submit.
    if (!control.touched && !control.dirty) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Required.';
    }
    if (name === 'newPassword' && control.hasError('minlength')) {
      return `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'newPassword' && control.hasError('maxlength')) {
      return `Password must be at most ${MAX_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'confirmPassword' && this.form.hasError('mismatch')) {
      return 'Passwords do not match.';
    }
    return null;
  }

  submit(): void {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.profileService.changePassword({ newPassword: this.form.controls.newPassword.value }).subscribe({
      next: () => {
        // The API ended every session, including this one, and the temporary password no longer works;
        // sign in again with the new password.
        this.authService.clearSession();
        void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.errorMessage.set(apiErrorMessage(err, 'Could not change your password.'));
      }
    });
  }
}
