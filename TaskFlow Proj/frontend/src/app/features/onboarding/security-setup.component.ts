import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import * as QRCode from 'qrcode';
import { AuthService } from '../../core/auth/auth.service';
import { dashboardRouteFor } from '../../core/auth/role-routes';
import { EMAIL_SUFFIX, localPartOf, localPartValidator, toEmail } from '../../core/email-domain';
import { IconComponent } from '../../core/layout/icon.component';
import { MOBILE_NUMBER_HINT, mobileNumberValidator, normalizeMobileNumber } from '../../core/mobile-number';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { EmailLocalPartDirective } from '../../shared/ui/email-local-part.directive';
import { initials } from '../../shared/ui/initials';
import { ToastService } from '../../shared/ui/toast.service';

export type SetupStep = 'details' | 'authenticator' | 'codes';

interface StepInfo {
  id: SetupStep;
  title: string;
  hint: string;
}

const STEPS: StepInfo[] = [
  { id: 'details', title: 'Your details', hint: 'Confirm who you are' },
  { id: 'authenticator', title: 'Authenticator app', hint: 'Add the second step' },
  { id: 'codes', title: 'Backup codes', hint: 'Keep a way back in' }
];

/** The first screen for anyone who has not set up two-step verification yet. Until it is finished the rest of the app (and
 * the API) stays closed: confirm your details and mobile number, connect an authenticator app, and save your backup codes. */
@Component({
  selector: 'app-security-setup',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent, EmailLocalPartDirective],
  template: `
    <div class="setup">
      <aside class="setup__aside" aria-label="Setup progress">
        <div class="setup__brand">
          <span class="setup__logo" aria-hidden="true">TF</span>
          <span class="setup__brand-name">TaskFlow</span>
        </div>

        <div class="setup__welcome">
          <h1>Let's secure your account</h1>
          <p>Two quick things before you start: confirm your details and connect an authenticator app.</p>
        </div>

        <ol class="setup-steps" data-field="steps">
          @for (item of steps; track item.id; let index = $index) {
            <li class="setup-steps__item" [attr.data-state]="stateOf(item.id)" [attr.data-step-id]="item.id">
              <span class="setup-steps__dot" aria-hidden="true">
                @if (stateOf(item.id) === 'done') {
                  <tf-icon name="check" />
                } @else {
                  {{ index + 1 }}
                }
              </span>
              <span class="setup-steps__text">
                <strong>{{ item.title }}</strong>
                <small>{{ item.hint }}</small>
              </span>
            </li>
          }
        </ol>

        <ul class="setup__perks">
          <li><tf-icon name="shield" /> Stops anyone using just your password</li>
          <li><tf-icon name="lock" /> Works offline, free, with any authenticator app</li>
          <li><tf-icon name="reset" /> Lets you recover a forgotten password yourself</li>
        </ul>
      </aside>

      <main class="setup__main">
        <div class="setup__mobile-progress" aria-hidden="true">
          <span class="setup__bar"><span class="setup__bar-fill" [style.width.%]="progress()"></span></span>
          <span class="setup__bar-label">Step {{ stepNumber() }} of {{ steps.length }}</span>
        </div>

        <section class="setup-card" [attr.aria-busy]="loading()">
          @if (loading()) {
            <div class="setup-skeleton" aria-hidden="true">
              <span class="tf-skeleton"></span><span class="tf-skeleton"></span><span class="tf-skeleton"></span>
            </div>
          } @else if (loadError()) {
            <div class="setup-alert" role="alert">
              <tf-icon name="alert" />
              <div>
                <strong>We could not load your details.</strong>
                <p>{{ loadError() }}</p>
                <button type="button" class="setup-btn setup-btn--ghost" data-action="retry" (click)="load()">Try again</button>
              </div>
            </div>
          } @else {
            @switch (step()) {
              @case ('details') {
                <form [formGroup]="detailsForm" (ngSubmit)="saveDetails()" novalidate data-step="details">
                  <header class="setup-card__head">
                    <span class="setup-avatar" aria-hidden="true" data-field="avatar">{{ avatar() }}</span>
                    <div>
                      <h2>Welcome, {{ firstName() }}</h2>
                      <p>Check your details and add a mobile number we can reach you on.</p>
                    </div>
                  </header>

                  <dl class="setup-facts" data-field="facts">
                    @for (fact of facts(); track fact.label) {
                      <div>
                        <dt>{{ fact.label }}</dt>
                        <dd [attr.data-field]="'fact-' + fact.label">{{ fact.value }}</dd>
                      </div>
                    }
                  </dl>

                  @if (error(); as message) {
                    <p class="setup-error" role="alert">{{ message }}</p>
                  }

                  <div class="setup-fields">
                    <label class="setup-field">
                      <span class="setup-field__label">Full name</span>
                      <input type="text" formControlName="fullName" autocomplete="name" data-field="fullName"
                        [attr.aria-invalid]="detailsForm.controls.fullName.touched && detailsForm.controls.fullName.invalid" />
                      @if (detailsForm.controls.fullName.touched && detailsForm.controls.fullName.invalid) {
                        <span class="setup-field__error" data-error="fullName">Enter your full name.</span>
                      }
                    </label>

                    <label class="setup-field">
                      <span class="setup-field__label">Email</span>
                      <span class="setup-input-group">
                        <input type="text" appEmailLocalPart formControlName="email" autocomplete="off" autocapitalize="none"
                          spellcheck="false" data-field="email"
                          [attr.aria-invalid]="detailsForm.controls.email.touched && detailsForm.controls.email.invalid" />
                        <span class="setup-input-group__suffix" data-email-suffix>{{ emailSuffix }}</span>
                      </span>
                      @if (detailsForm.controls.email.touched && detailsForm.controls.email.invalid) {
                        <span class="setup-field__error" data-error="email">Use letters, digits, dot, underscore or hyphen before {{ emailSuffix }}.</span>
                      }
                    </label>

                    <label class="setup-field">
                      <span class="setup-field__label">Mobile number</span>
                      <input type="tel" inputmode="tel" autocomplete="tel" placeholder="+919876543210"
                        formControlName="mobileNumber" data-field="mobileNumber"
                        [attr.aria-invalid]="detailsForm.controls.mobileNumber.touched && detailsForm.controls.mobileNumber.invalid" />
                      @if (detailsForm.controls.mobileNumber.touched && detailsForm.controls.mobileNumber.hasError('required')) {
                        <span class="setup-field__error" data-error="mobileNumber">Enter your mobile number.</span>
                      } @else if (detailsForm.controls.mobileNumber.touched && detailsForm.controls.mobileNumber.invalid) {
                        <span class="setup-field__error" data-error="mobileNumber">{{ mobileHint }}</span>
                      } @else {
                        <span class="setup-field__hint">{{ mobileHint }}</span>
                      }
                    </label>
                  </div>

                  <footer class="setup-actions">
                    <button type="button" class="setup-btn setup-btn--link" data-action="sign-out" (click)="signOut()">Sign out</button>
                    <button type="submit" class="setup-btn setup-btn--primary" data-action="save-details" [disabled]="busy()">
                      {{ busy() ? 'Saving…' : 'Save and continue' }}
                    </button>
                  </footer>
                </form>
              }

              @case ('authenticator') {
                <div data-step="authenticator">
                  <header class="setup-card__head">
                    <span class="setup-badge" aria-hidden="true"><tf-icon name="shield" /></span>
                    <div>
                      <h2>Connect your authenticator app</h2>
                      <p>Use Google Authenticator, Microsoft Authenticator, Authy or any TOTP app on your phone.</p>
                    </div>
                  </header>

                  @if (error(); as message) {
                    <p class="setup-error" role="alert">{{ message }}</p>
                  }

                  @if (!scanning()) {
                    <form [formGroup]="passwordForm" (ngSubmit)="startAuthenticator()" novalidate data-step="authenticator-password">
                      <p class="setup-note">First confirm it is you with the password you just signed in with.</p>
                      <label class="setup-field">
                        <span class="setup-field__label">Current password</span>
                        <span class="setup-input-group">
                          <input [type]="showPassword() ? 'text' : 'password'" formControlName="currentPassword"
                            autocomplete="current-password" data-field="currentPassword" />
                          <button type="button" class="setup-input-group__button" data-action="toggle-password"
                            [attr.aria-pressed]="showPassword()" (click)="showPassword.set(!showPassword())">
                            {{ showPassword() ? 'Hide' : 'Show' }}
                          </button>
                        </span>
                        @if (passwordForm.controls.currentPassword.touched && passwordForm.controls.currentPassword.invalid) {
                          <span class="setup-field__error" data-error="currentPassword">Enter your password.</span>
                        }
                      </label>
                      <footer class="setup-actions">
                        <button type="button" class="setup-btn setup-btn--ghost" data-action="back" (click)="backToDetails()">Back</button>
                        <button type="submit" class="setup-btn setup-btn--primary" data-action="start-authenticator" [disabled]="busy()">
                          {{ busy() ? 'Please wait…' : 'Show my QR code' }}
                        </button>
                      </footer>
                    </form>
                  } @else {
                    <form [formGroup]="codeForm" (ngSubmit)="confirmAuthenticator()" novalidate data-step="authenticator-scan">
                      <div class="setup-scan">
                        @if (qrDataUrl(); as qr) {
                          <img class="setup-scan__qr" [src]="qr" alt="QR code to add TaskFlow to your authenticator app" data-field="qr" />
                        }
                        <div class="setup-scan__side">
                          <ol class="setup-instructions">
                            <li>Open your authenticator app and choose <strong>Add account</strong>.</li>
                            <li>Scan the QR code, or type the key below.</li>
                            <li>Enter the 6-digit code the app shows.</li>
                          </ol>
                          <div class="setup-key">
                            <code data-field="key">{{ groupedKey() }}</code>
                            <button type="button" class="setup-btn setup-btn--ghost setup-btn--small" data-action="copy-key" (click)="copyKey()">Copy key</button>
                          </div>
                        </div>
                      </div>

                      <label class="setup-field setup-field--code">
                        <span class="setup-field__label">6-digit code</span>
                        <input type="text" inputmode="numeric" autocomplete="one-time-code" maxlength="6" placeholder="123456"
                          formControlName="code" data-field="code"
                          [attr.aria-invalid]="codeForm.controls.code.touched && codeForm.controls.code.invalid" />
                        @if (codeForm.controls.code.touched && codeForm.controls.code.invalid) {
                          <span class="setup-field__error" data-error="code">Enter the 6-digit code from the app.</span>
                        }
                      </label>

                      <footer class="setup-actions">
                        <button type="button" class="setup-btn setup-btn--ghost" data-action="restart" (click)="restartAuthenticator()">Start over</button>
                        <button type="submit" class="setup-btn setup-btn--primary" data-action="confirm-authenticator" [disabled]="busy()">
                          {{ busy() ? 'Checking…' : 'Verify and turn on' }}
                        </button>
                      </footer>
                    </form>
                  }
                </div>
              }

              @case ('codes') {
                <div data-step="codes">
                  <header class="setup-card__head">
                    <span class="setup-badge setup-badge--success" aria-hidden="true"><tf-icon name="check" /></span>
                    <div>
                      <h2>Two-step verification is on</h2>
                      <p>Save these backup codes. Each one works once if you ever lose your phone. They are shown only now.</p>
                    </div>
                  </header>

                  <ul class="setup-codes" data-field="backupCodes">
                    @for (code of backupCodes(); track code) {
                      <li>{{ code }}</li>
                    }
                  </ul>

                  <div class="setup-code-actions">
                    <button type="button" class="setup-btn setup-btn--ghost" data-action="copy-codes" (click)="copyCodes()">Copy all</button>
                    <button type="button" class="setup-btn setup-btn--ghost" data-action="download-codes" (click)="downloadCodes()">Download</button>
                  </div>

                  <label class="setup-check">
                    <input type="checkbox" data-field="saved" [checked]="savedCodes()" (change)="savedCodes.set(!savedCodes())" />
                    <span>I have saved my backup codes somewhere safe.</span>
                  </label>

                  <footer class="setup-actions">
                    <button type="button" class="setup-btn setup-btn--primary" data-action="finish" [disabled]="!savedCodes()" (click)="finish()">
                      Open TaskFlow
                    </button>
                  </footer>
                </div>
              }
            }
          }
        </section>
      </main>
    </div>
  `,
  styleUrl: './security-setup.component.css'
})
export class SecuritySetupComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly steps = STEPS;
  readonly emailSuffix = EMAIL_SUFFIX;
  readonly mobileHint = MOBILE_NUMBER_HINT;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly profile = signal<User | null>(null);

  readonly step = signal<SetupStep>('details');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly showPassword = signal(false);
  /** Setup has produced a key, so the QR code and code box are showing. */
  readonly scanning = signal(false);
  readonly qrDataUrl = signal<string | null>(null);
  readonly backupCodes = signal<string[]>([]);
  readonly savedCodes = signal(false);
  private key = '';

  readonly detailsForm = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    // Only the part before the fixed domain; the suffix is shown beside it and added on save.
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, localPartValidator] }),
    mobileNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, mobileNumberValidator] })
  });

  readonly passwordForm = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  readonly codeForm = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)] })
  });

  readonly avatar = computed(() => initials(this.profile()?.fullName));
  readonly firstName = computed(() => (this.profile()?.fullName ?? '').trim().split(/\s+/)[0] || 'there');
  readonly stepNumber = computed(() => STEPS.findIndex((item) => item.id === this.step()) + 1);
  readonly progress = computed(() => (this.stepNumber() / STEPS.length) * 100);

  /** The read-only employee details shown above the form. */
  readonly facts = computed(() => {
    const user = this.profile();
    return user
      ? [
          { label: 'Employee code', value: user.employeeCode || '—' },
          { label: 'Username', value: user.username },
          { label: 'Role', value: user.role },
          { label: 'Project', value: user.projectName || '—' },
          { label: 'Manager', value: user.managerName || '—' }
        ]
      : [];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.profileService.get().subscribe({
      next: (user) => {
        this.profile.set(user);
        this.detailsForm.reset({
          fullName: user.fullName,
          email: localPartOf(user.email),
          mobileNumber: user.mobileNumber ?? ''
        });
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Please check your connection and try again.'));
        this.loading.set(false);
      }
    });
  }

  stateOf(id: SetupStep): 'done' | 'current' | 'todo' {
    const current = STEPS.findIndex((item) => item.id === this.step());
    const index = STEPS.findIndex((item) => item.id === id);
    return index < current ? 'done' : index === current ? 'current' : 'todo';
  }

  groupedKey(): string {
    return this.key.replace(/(.{4})/g, '$1 ').trim();
  }

  saveDetails(): void {
    if (this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    if (this.busy()) {
      return;
    }

    const value = this.detailsForm.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.profileService
      .update({
        fullName: value.fullName.trim(),
        email: toEmail(value.email),
        mobileNumber: normalizeMobileNumber(value.mobileNumber)
      })
      .subscribe({
        next: (user) => {
          this.busy.set(false);
          this.profile.set(user);
          this.authService.applyProfile({
            id: user.id,
            fullName: user.fullName,
            username: user.username,
            email: user.email,
            role: user.role,
            employeeCode: user.employeeCode ?? null
          });
          this.step.set('authenticator');
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.error.set(apiErrorMessage(err, 'Could not save your details. Please try again.'));
        }
      });
  }

  backToDetails(): void {
    this.error.set(null);
    this.step.set('details');
  }

  startAuthenticator(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.profileService.startMfaSetup(this.passwordForm.controls.currentPassword.value).subscribe({
      next: async (setup) => {
        this.key = setup.key;
        try {
          this.qrDataUrl.set(await QRCode.toDataURL(setup.provisioningUri, { width: 220, margin: 1 }));
        } catch {
          // The typed key is shown as well, so setup still works without the picture.
          this.qrDataUrl.set(null);
        }

        this.scanning.set(true);
        this.passwordForm.reset();
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err, 'Could not start setup. Please try again.'));
      }
    });
  }

  restartAuthenticator(): void {
    this.key = '';
    this.scanning.set(false);
    this.qrDataUrl.set(null);
    this.codeForm.reset();
    this.error.set(null);
  }

  confirmAuthenticator(): void {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.profileService.enableMfa(this.codeForm.controls.code.value).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.key = '';
        this.scanning.set(false);
        this.qrDataUrl.set(null);
        this.backupCodes.set(result.codes);
        this.step.set('codes');
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.codeForm.controls.code.reset('');
        this.error.set(apiErrorMessage(err, 'That code did not work. Check the app and try the new code.'));
      }
    });
  }

  async copyKey(): Promise<void> {
    await this.copy(this.key, 'Key copied');
  }

  async copyCodes(): Promise<void> {
    await this.copy(this.backupCodes().join('\n'), 'Backup codes copied');
  }

  downloadCodes(): void {
    const text = `TaskFlow backup codes\nEach code works once.\n\n${this.backupCodes().join('\n')}\n`;
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'taskflow-backup-codes.txt';
    link.click();
    URL.revokeObjectURL(url);
  }

  finish(): void {
    if (!this.savedCodes()) {
      return;
    }

    this.backupCodes.set([]);
    this.authService.markMfaConfigured();
    const role = this.authService.currentUser()?.role ?? this.profile()?.role ?? 'Employee';
    void this.router.navigateByUrl(dashboardRouteFor(role));
  }

  signOut(): void {
    this.authService.logout().subscribe(() => {
      void this.router.navigate(['/login']);
    });
  }

  private async copy(text: string, message: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toast.success(message);
    } catch {
      this.toast.error('Could not copy. Select the text and copy it by hand.');
    }
  }
}
