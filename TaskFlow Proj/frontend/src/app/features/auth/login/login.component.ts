import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, catchError, exhaustMap, filter, interval } from 'rxjs';
import { LOGIN_REASON_MESSAGES } from '../../../core/auth/login-reasons';
import { readRememberedIdentifier, rememberIdentifier } from '../../../core/auth/remembered-identifier';
import { dashboardRouteFor } from '../../../core/auth/role-routes';
import { AuthService } from '../../../core/auth/auth.service';
import { PasswordResetService } from '../../../core/services/password-reset.service';
import { PASSWORD_RESET_CONFIRMATION } from '../../../shared/ui/password-reset-text';

/** How often the page checks for a reset notice while it knows the account (design D12). */
export const NOTICE_POLL_MS = 2000;

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly passwordResetService = inject(PasswordResetService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** Why the user was sent here (e.g. their credentials changed), from the `reason` query parameter. */
  private readonly route = inject(ActivatedRoute);
  readonly notice = LOGIN_REASON_MESSAGES[this.route.snapshot.queryParamMap.get('reason') ?? ''] ?? null;
  readonly showPassword = signal(false);

  /** A new credential is waiting for the identifier typed in the form (cleared by signing in). */
  readonly credentialNotice = this.passwordResetService.noticeAvailable;
  /** The encrypted login-link token from the address, if the page was opened through one (design D14). */
  private loginToken: string | null = null;
  /** The password field, focused when the bell points the user at signing in. */
  private readonly passwordInput = viewChild<ElementRef<HTMLInputElement>>('passwordInput');
  /** Shown after the bell is used on a page without a link: sign in with the password the administrator gave. */
  readonly signInHint = signal(false);

  readonly forgotOpen = signal(false);
  readonly forgotSubmitting = signal(false);
  /** The generic confirmation, shown for every submission whether or not an account matched. */
  readonly forgotConfirmation = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    emailOrUsername: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  readonly forgotForm = this.formBuilder.group({
    identifier: ['', [Validators.required, Validators.maxLength(256)]]
  });

  constructor() {
    const token = this.route.snapshot.paramMap?.get('token')?.trim() || null;
    if (token) {
      // The address carries only an encrypted token; the page resolves it and shows the email, so the email is
      // never in the address bar. A token that does not resolve behaves like plain /login.
      this.passwordResetService.resolveLink(token).subscribe({
        next: (resolution) => {
          if (resolution.email) {
            this.loginToken = token;
            this.form.controls.emailOrUsername.setValue(resolution.email);
          } else {
            this.useRememberedIdentifier();
          }
        },
        error: () => this.useRememberedIdentifier()
      });
    } else {
      this.useRememberedIdentifier();
    }

    // The page never refreshes itself, so it asks again every couple of seconds while it knows the account and
    // the tab is visible: the bell appears soon after the administrator finishes, with no reload, and goes away
    // once the notice is cleared. One request at a time; a failed check keeps what is shown.
    interval(NOTICE_POLL_MS)
      .pipe(
        filter(() => !document.hidden && this.form.controls.emailOrUsername.value.trim() !== ''),
        exhaustMap(() =>
          this.passwordResetService.checkNotice(this.form.controls.emailOrUsername.value).pipe(catchError(() => EMPTY))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  /** Looks up the credential notice for the identifier just typed. A failed lookup shows nothing. */
  checkNotice(): void {
    this.passwordResetService.checkNotice(this.form.controls.emailOrUsername.value).subscribe({
      error: () => this.passwordResetService.clearNotice()
    });
  }

  /** Plain /login pre-fills the identifier last used on this device and looks its notice up. */
  private useRememberedIdentifier(): void {
    const identifier = readRememberedIdentifier();
    if (identifier) {
      this.form.controls.emailOrUsername.setValue(identifier);
      this.checkNotice();
    }
  }

  /** The bell. Through an encrypted link it opens the password update screen (the link travels in router state,
   * never in the address bar). Without a link nothing proves who is at the page, so it points the user at signing
   * in with the password the administrator gave them; that leads to the same New/Confirm screen. */
  openResetScreen(): void {
    const email = this.form.controls.emailOrUsername.value.trim();
    if (this.loginToken) {
      void this.router.navigate(['/reset-password'], { state: { token: this.loginToken, email } });
      return;
    }

    // No link: the password typed here (the one the administrator gave) is the proof. It travels with the account
    // to the update screen, which shows only New/Confirm password.
    const typed = this.form.controls.password.value;
    if (email !== '' && typed !== '') {
      void this.router.navigate(['/reset-password'], { state: { email, currentPassword: typed } });
      return;
    }

    this.signInHint.set(true);
    this.passwordInput()?.nativeElement.focus();
  }

  openForgot(): void {
    this.forgotForm.reset({ identifier: this.form.controls.emailOrUsername.value.trim() });
    this.forgotConfirmation.set(null);
    this.forgotOpen.set(true);
  }

  closeForgot(): void {
    this.forgotOpen.set(false);
  }

  submitForgot(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    if (this.forgotSubmitting()) {
      return;
    }

    this.forgotSubmitting.set(true);
    const done = () => {
      this.forgotSubmitting.set(false);
      // The same text for every outcome: an error must not reveal whether an account exists either.
      this.forgotConfirmation.set(PASSWORD_RESET_CONFIRMATION);
    };

    this.passwordResetService.requestFromLogin(this.forgotForm.controls.identifier.value).subscribe({
      next: done,
      error: done
    });
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
        // The server cleared the notice with this sign-in; drop it here too.
        this.passwordResetService.clearNotice();
        void this.router.navigateByUrl(dashboardRouteFor(user.role));
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Invalid email/username or password.');
      }
    });
  }
}
