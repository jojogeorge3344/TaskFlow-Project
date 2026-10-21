import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../../core/auth/login-reasons';
import { PasswordResetService } from '../../../core/services/password-reset.service';
import { ProfileService } from '../../../core/services/profile.service';
import { apiErrorMessage } from '../../../shared/ui/api-error';

const MIN_PASSWORD_LENGTH = 6;
/** Mirrors the server rule: sign-in rejects passwords longer than this, so the form must too. */
const MAX_PASSWORD_LENGTH = 128;

/** The link the bell was opened from, carried in router state - never in the address bar. */
interface ResetTarget {
  /** Absent when the bell was used on a page without a link: the temporary password is then the proof. */
  token?: string;
  email?: string;
  /** The password already typed on the Login Screen, to pre-fill the field; removed from history once read. */
  currentPassword?: string;
}

function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as {
    newPassword: string;
    confirmPassword: string;
  };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

type Field = 'newPassword' | 'confirmPassword';

/** The password update screen the Login Screen bell opens when the page was reached through an encrypted link
 * (design D14): New password and Confirm password only - the link is the proof, so the temporary password is never
 * asked for, shown or pre-filled. It offers no cancel, close or link out; the only way forward is a successful
 * update, after which the user signs in with the new password. */
@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule],
  templateUrl: './reset-password.component.html',
  styleUrl: '../login/login.component.css'
})
export class ResetPasswordComponent {
  private readonly passwordResetService = inject(PasswordResetService);
  private readonly authService = inject(AuthService);
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);

  /** Navigation state survives a reload (it lives in the history entry); without it there is nobody to update. */
  private readonly target: ResetTarget | null = this.readTarget();

  readonly email = this.target?.email ?? '';
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

  constructor() {
    if (this.target === null) {
      // Opened directly rather than from a link: there is no account to update.
      void this.router.navigate(['/login']);
    }
  }

  fieldError(name: Field): string | null {
    const control = this.form.controls[name];
    // Typing is enough to show a problem (the "do not match" message appears while confirming).
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

    if (this.submitting() || this.target === null) {
      return;
    }

    const { newPassword } = this.form.getRawValue();
    this.submitting.set(true);

    const done = () => {
      void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
    };
    const failed = (err: unknown) => {
      this.submitting.set(false);
      this.errorMessage.set(this.messageFor(err));
    };

    if (this.target.token) {
      this.passwordResetService
        .completeFromLogin({ token: this.target.token, newPassword })
        .subscribe({ next: done, error: failed });
      return;
    }

    // No link: sign in with the temporary password (the proof), set the new password, then end that session.
    // The current password is sent too: an account the administrator set a normal password for is not flagged
    // "must change", and the API then checks the current password before replacing it.
    const currentPassword = this.target.currentPassword ?? '';
    let signedIn = false;
    this.authService
      .login({ emailOrUsername: this.email, password: currentPassword })
      .pipe(
        tap(() => (signedIn = true)),
        switchMap(() => this.profileService.changePassword({ currentPassword, newPassword }))
      )
      .subscribe({
        next: () => {
          this.authService.clearSession();
          done();
        },
        error: (err: unknown) => {
          this.authService.clearSession();
          if (!signedIn && err instanceof HttpErrorResponse && err.status === 401) {
            this.submitting.set(false);
            this.errorMessage.set('The password typed on the sign-in page was not accepted. Go back and enter the password your administrator gave you.');
            return;
          }
          failed(err);
        }
      });
  }

  /** One generic message for a refusal, so nothing about the account leaks; the rest are shown as the API says. */
  private messageFor(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 401) {
        return 'The link or current password was not accepted.';
      }
      if (err.status === 429) {
        return 'Too many attempts. Please wait a minute and try again.';
      }
    }
    return apiErrorMessage(err, 'Could not update your password.');
  }

  private readTarget(): ResetTarget | null {
    const state = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as ResetTarget | null;
    if (state?.currentPassword) {
      // Keep the password out of the history entry once it has been read.
      const { currentPassword: _removed, ...rest } = state;
      history.replaceState(rest, '');
    }
    // Without a link the password typed on the Login Screen is the proof; after a reload it is gone, so start over.
    return state?.token || (state?.email && state.currentPassword) ? state : null;
  }
}
