import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../shared/auth/auth.service';
import { LOGIN_REASON_PASSWORD_RESET } from '../../../../shared/auth/login-reasons';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { LegalFooterComponent } from '../../../../shared/components/legal-footer/legal-footer.component';
import { RecoveryStep } from './account-recovery.model';

const MIN_PASSWORD_LENGTH = 6;
const MAX_PASSWORD_LENGTH = 128;

function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as {
    newPassword: string;
    confirmPassword: string;
  };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

/** Forgot password from the sign-in page, for people who turned on the authenticator app: enter your username or email and
 * the current 6-digit code, then set and confirm a new password. Every failure looks the same, so the page never says
 * whether an account exists or has the app set up. */
@Component({
  selector: 'app-account-recovery',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, LegalFooterComponent],
  template: `
    <section class="fp">
      <div class="fp__card">
        <header class="fp__header">
          <div class="fp__logo" aria-hidden="true">TF</div>
          <h1 class="fp__title">Forgot your password?</h1>
          @if (step() === 'verify') {
            <p class="fp__subtitle">
              Confirm it is you with the 6-digit code from your authenticator app, then choose a new password.
            </p>
          } @else {
            <p class="fp__subtitle">Code verified. Choose a new password for your account.</p>
          }
        </header>

        @if (step() === 'verify') {
          <form class="fp__form" [formGroup]="verifyForm" (ngSubmit)="verify()" novalidate data-step="verify">
            <label class="fp__field">
              <span class="fp__label">Email or username</span>
              <input
                type="text"
                formControlName="identifier"
                autocomplete="username"
                data-field="identifier"
                [attr.aria-invalid]="verifyForm.controls.identifier.invalid && verifyForm.controls.identifier.touched"
              />
              @if (verifyForm.controls.identifier.touched && verifyForm.controls.identifier.invalid) {
                <span class="fp__hint" data-error="identifier">Email or username is required.</span>
              }
            </label>

            <label class="fp__field">
              <span class="fp__label">Code from your authenticator app</span>
              <input
                type="text"
                formControlName="code"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="6"
                placeholder="123456"
                data-field="code"
                [attr.aria-invalid]="verifyForm.controls.code.invalid && verifyForm.controls.code.touched"
              />
              @if (verifyForm.controls.code.touched && verifyForm.controls.code.invalid) {
                <span class="fp__hint" data-error="code">Enter the 6-digit code from the app.</span>
              }
            </label>

            @if (error(); as message) {
              <p class="fp__error" role="alert">{{ message }}</p>
            }

            <button type="submit" class="fp__submit" data-action="verify" [disabled]="busy()">
              {{ busy() ? 'Checking…' : 'Continue' }}
            </button>

            <p class="fp__note">
              This works if you turned on two-step verification from your Profile. If you did not, ask an administrator to
              set a new password for you.
            </p>
          </form>
        } @else {
          <form class="fp__form" [formGroup]="passwordForm" (ngSubmit)="resetPassword()" novalidate data-step="password">
            <label class="fp__field">
              <span class="fp__label">New password</span>
              <input
                type="password"
                formControlName="newPassword"
                autocomplete="new-password"
                maxlength="128"
                data-field="newPassword"
                [attr.aria-invalid]="passwordForm.controls.newPassword.invalid && passwordForm.controls.newPassword.touched"
              />
              @if (passwordForm.controls.newPassword.touched && passwordForm.controls.newPassword.invalid) {
                <span class="fp__hint" data-error="newPassword">
                  Use at least {{ minPasswordLength }} and at most {{ maxPasswordLength }} characters.
                </span>
              }
            </label>

            <label class="fp__field">
              <span class="fp__label">Confirm new password</span>
              <input
                type="password"
                formControlName="confirmPassword"
                autocomplete="new-password"
                maxlength="128"
                data-field="confirmPassword"
                [attr.aria-invalid]="passwordForm.controls.confirmPassword.touched && passwordForm.hasError('mismatch')"
              />
              @if (passwordForm.controls.confirmPassword.touched && passwordForm.hasError('mismatch')) {
                <span class="fp__hint" data-error="confirmPassword">Passwords do not match.</span>
              }
            </label>

            @if (error(); as message) {
              <p class="fp__error" role="alert">{{ message }}</p>
            }

            <button type="submit" class="fp__submit" data-action="reset-password" [disabled]="busy()">
              {{ busy() ? 'Saving…' : 'Reset password' }}
            </button>

            @if (tokenRejected()) {
              <button type="button" class="fp__link" data-action="start-over" (click)="startOver()">Start over</button>
            }
          </form>
        }

        <a class="fp__back" routerLink="/login">← Back to sign in</a>
      </div>
      <app-legal-footer variant="dark" />
    </section>
  `,
  styles: `
    .fp {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1.5rem;
      padding: 1.5rem;
      background: #0f172a;
      /* Fixed light card on a dark page: keep the Default colours whichever theme is saved. */
      color-scheme: light;
      color: #0f172a;
      --tf-primary: #2563eb;
      --tf-primary-hover: #1d4ed8;
      --tf-on-primary: #ffffff;
      --tf-surface: #ffffff;
      --tf-bg: #f1f5f9;
      --tf-border: #e2e8f0;
      --tf-text: #0f172a;
      --tf-text-secondary: #475569;
      --tf-muted: #64748b;
      --tf-label: #334155;
      --tf-field-bg: #f8fafc;
      --tf-field-border: #e2e8f0;
      --tf-danger: #dc2626;
      --tf-danger-strong: #b91c1c;
      --tf-danger-soft: #fee2e2;
    }

    .fp__card {
      width: 100%;
      max-width: 26rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      padding: 2rem;
      border-radius: 1rem;
      background: #ffffff;
      box-shadow: 0 20px 45px rgba(15, 23, 42, 0.35);
    }

    .fp__header {
      text-align: center;
    }

    .fp__logo {
      width: 3rem;
      height: 3rem;
      margin: 0 auto 0.75rem;
      display: grid;
      place-items: center;
      border-radius: 0.75rem;
      background: #2563eb;
      color: #ffffff;
      font-weight: 700;
    }

    .fp__title {
      margin: 0;
      font-size: 1.375rem;
      color: #0f172a;
    }

    .fp__subtitle {
      margin: 0.5rem 0 0;
      color: #64748b;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .fp__form {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .fp__field {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .fp__label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: #334155;
    }

    .fp__field input {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 0.5rem;
      font-size: 0.9375rem;
      box-sizing: border-box;
    }

    .fp__field input:focus {
      outline: 2px solid #2563eb;
      outline-offset: 1px;
    }

    .fp__hint {
      font-size: 0.75rem;
      color: #b91c1c;
    }

    .fp__error {
      margin: 0;
      padding: 0.625rem 0.75rem;
      border-radius: 0.5rem;
      background: #fef2f2;
      color: #b91c1c;
      font-size: 0.8125rem;
    }

    .fp__submit {
      padding: 0.75rem;
      border: none;
      border-radius: 0.5rem;
      background: #2563eb;
      color: #ffffff;
      font-size: 0.9375rem;
      font-weight: 600;
      cursor: pointer;
    }

    .fp__submit:hover:not(:disabled) {
      background: #1d4ed8;
    }

    .fp__submit:disabled {
      opacity: 0.7;
      cursor: progress;
    }

    .fp__note {
      margin: 0;
      color: #64748b;
      font-size: 0.75rem;
      line-height: 1.5;
      text-align: center;
    }

    .fp__link {
      padding: 0;
      border: none;
      background: none;
      font-size: 0.8125rem;
      font-weight: 600;
      color: #2563eb;
      cursor: pointer;
    }

    .fp__link:hover {
      text-decoration: underline;
    }

    .fp__back {
      align-self: center;
      font-size: 0.875rem;
      font-weight: 600;
      color: #2563eb;
      text-decoration: none;
    }

    .fp__back:hover {
      text-decoration: underline;
    }

    @media (max-width: 28rem) {
      .fp {
        padding: 1rem;
      }

      .fp__card {
        padding: 1.5rem 1.25rem;
      }
    }
  `
})
export class AccountRecoveryComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  readonly maxPasswordLength = MAX_PASSWORD_LENGTH;

  readonly step = signal<RecoveryStep>('verify');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  /** The reset token was refused (used or expired), so the only way forward is to start again. */
  readonly tokenRejected = signal(false);

  readonly verifyForm = new FormGroup({
    identifier: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)] })
  });

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

  private resetToken = '';

  verify(): void {
    if (this.verifyForm.invalid) {
      this.verifyForm.markAllAsTouched();
      return;
    }

    if (this.busy()) {
      return;
    }

    const { identifier, code } = this.verifyForm.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.authService.verifyAuthenticatorRecovery(identifier.trim(), code).subscribe({
      next: (response) => {
        this.busy.set(false);
        this.resetToken = response.resetToken;
        this.verifyForm.controls.code.reset('');
        this.step.set('password');
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.verifyForm.controls.code.reset('');
        this.error.set(apiErrorMessage(err, 'Could not verify the code. Please try again.'));
      }
    });
  }

  resetPassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    if (this.busy()) {
      return;
    }

    const { newPassword, confirmPassword } = this.passwordForm.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.authService.recoverPassword({ resetToken: this.resetToken, newPassword, confirmPassword }).subscribe({
      next: () => {
        this.busy.set(false);
        void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_PASSWORD_RESET } });
      },
      error: (err: unknown) => {
        this.busy.set(false);
        // A validation problem leaves the token usable; a 400 with a plain message means it is spent or expired.
        const validation = (err as { error?: { errors?: unknown } } | null)?.error?.errors;
        this.tokenRejected.set(!validation);
        this.error.set(apiErrorMessage(err, 'Could not reset your password. Please try again.'));
      }
    });
  }

  startOver(): void {
    this.resetToken = '';
    this.tokenRejected.set(false);
    this.passwordForm.reset();
    this.verifyForm.reset();
    this.error.set(null);
    this.step.set('verify');
  }
}
