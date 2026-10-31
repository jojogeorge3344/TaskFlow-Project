import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LOGIN_REASON_MESSAGES } from '../../../core/auth/login-reasons';
import { readRememberedIdentifier, rememberIdentifier } from '../../../core/auth/remembered-identifier';
import { dashboardRouteFor } from '../../../core/auth/role-routes';
import { AuthService } from '../../../core/auth/auth.service';
import { isMfaChallenge } from '../../../core/auth/auth.models';
import { AuthUser } from '../../../core/auth/auth.models';
import { LegalFooterComponent } from '../../legal/legal-footer.component';
import { LoginBackgroundComponent } from './login-background.component';
import { apiErrorMessage } from '../../../shared/ui/api-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, LegalFooterComponent, LoginBackgroundComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** Why the user was sent here (e.g. their credentials changed), from the `reason` query parameter. */
  readonly notice = LOGIN_REASON_MESSAGES[inject(ActivatedRoute).snapshot.queryParamMap.get('reason') ?? ''] ?? null;
  readonly showPassword = signal(false);
  /** The challenge from the password step when the account has MFA on; null while the password step is showing. */
  readonly challenge = signal<string | null>(null);
  readonly mfaForm = this.formBuilder.group({
    code: ['', [Validators.required]]
  });

  /** Where to go after signing in, when the user was sent here from a page they asked for. Only in-app paths count. */
  private readonly returnUrl = this.safeReturnUrl(inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'));

  readonly form = this.formBuilder.group({
    emailOrUsername: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  constructor() {
    // The Login Screen pre-fills the identifier last used on this device; a per-viewer convenience only.
    const identifier = readRememberedIdentifier();
    if (identifier) {
      this.form.controls.emailOrUsername.setValue(identifier);
    }
  }

  togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: (outcome) => {
        this.submitting.set(false);
        rememberIdentifier(this.form.controls.emailOrUsername.value);
        if (isMfaChallenge(outcome)) {
          this.mfaForm.reset();
          this.challenge.set(outcome.challengeToken);
          return;
        }

        this.finishSignIn(outcome);
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Invalid email or password.');
      }
    });
  }


  /** Second step: the 6-digit code from the authenticator app. */
  submitCode(): void {
    const challenge = this.challenge();
    if (challenge === null) {
      return;
    }

    const code = this.mfaForm.controls.code.value.trim();
    if (!code) {
      this.mfaForm.markAllAsTouched();
      return;
    }

    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.authService.verifyMfa(challenge, code).subscribe({
      next: (user) => {
        this.submitting.set(false);
        this.finishSignIn(user);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.mfaForm.controls.code.reset('');
        this.errorMessage.set(apiErrorMessage(err, 'That code did not work. Check the code and try again.'));
      }
    });
  }

  /** Back to the password step, for example after the sign-in expired or the wrong account was entered. */
  cancelMfa(): void {
    this.challenge.set(null);
    this.mfaForm.reset();
    this.errorMessage.set(null);
    this.form.controls.password.reset('');
  }

  private finishSignIn(user: AuthUser): void {
    void this.router.navigateByUrl(this.returnUrl ?? dashboardRouteFor(user.role, user.permissions));
  }

  private safeReturnUrl(value: string | null): string | null {
    return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/login') ? value : null;
  }
}
