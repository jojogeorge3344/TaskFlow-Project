import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LOGIN_REASON_MESSAGES } from '../../../core/auth/login-reasons';
import { readRememberedIdentifier, rememberIdentifier } from '../../../core/auth/remembered-identifier';
import { dashboardRouteFor } from '../../../core/auth/role-routes';
import { AuthService } from '../../../core/auth/auth.service';
import { LegalFooterComponent } from '../../legal/legal-footer.component';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, LegalFooterComponent],
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

  readonly form = this.formBuilder.group({
    emailOrUsername: ['', [Validators.required]],
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
      next: (user) => {
        this.submitting.set(false);
        rememberIdentifier(this.form.controls.emailOrUsername.value);
        void this.router.navigateByUrl(dashboardRouteFor(user.role));
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Invalid email/username or password.');
      }
    });
  }
}
