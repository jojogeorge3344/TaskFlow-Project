import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../core/layout/icon.component';
import { EmailDelivery, EmailSettings, SmtpSecurity, TestEmailResult } from '../../core/models';
import { EmailSettingsService } from '../../core/services/email-settings.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

const SEND_TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

/** Mirrors the server rule: a server and sender address are required while email is enabled. */
function requiredWhenEnabled(group: AbstractControl): ValidationErrors | null {
  const { emailEnabled, smtpHost, senderAddress } = (group as FormGroup).getRawValue() as {
    emailEnabled: boolean;
    smtpHost: string;
    senderAddress: string;
  };
  if (!emailEnabled) {
    return null;
  }
  const errors: ValidationErrors = {};
  if (!smtpHost?.trim()) {
    errors['hostRequired'] = true;
  }
  if (!senderAddress?.trim()) {
    errors['senderRequired'] = true;
  }
  return Object.keys(errors).length ? errors : null;
}

type EmailField = 'smtpHost' | 'smtpPort' | 'smtpSecurity' | 'senderAddress' | 'senderName' | 'smtpUsername' | 'sendTime';

export const SMTP_SECURITY_OPTIONS: { value: SmtpSecurity; label: string }[] = [
  { value: 'StartTls', label: 'STARTTLS (usually port 587)' },
  { value: 'SslOnConnect', label: 'SSL/TLS (usually port 465)' },
  { value: 'Auto', label: 'Automatic' },
  { value: 'None', label: 'None (local test servers only)' }
];

/** Email Configuration (Screen 14). Administrator only; validation mirrors UpdateEmailSettingsRequestValidator.
 * The SMTP password is never entered here: it lives in server configuration. */
@Component({
  selector: 'app-email-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './email-settings.component.html',
  styleUrl: './email-settings.component.css'
})
export class EmailSettingsComponent implements OnInit {
  private readonly settingsService = inject(EmailSettingsService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly securityOptions = SMTP_SECURITY_OPTIONS;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly settings = signal<EmailSettings | null>(null);
  /** Configuration problems, shown while email is switched on. */
  readonly visibleIssues = computed(() => {
    const settings = this.settings();
    return settings?.emailEnabled ? settings.issues : [];
  });

  readonly testing = signal(false);
  readonly testResult = signal<TestEmailResult | null>(null);
  readonly testAddress = new FormControl(this.authService.currentUser()?.email ?? '', {
    nonNullable: true,
    validators: [Validators.email]
  });

  readonly deliveries = signal<EmailDelivery[]>([]);
  readonly deliveriesLoading = signal(true);
  readonly deliveriesError = signal<string | null>(null);

  readonly form = new FormGroup(
    {
      emailEnabled: new FormControl(false, { nonNullable: true }),
      smtpHost: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(255)] }),
      smtpPort: new FormControl(587, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(1), Validators.max(65535)]
      }),
      smtpSecurity: new FormControl<SmtpSecurity>('StartTls', { nonNullable: true }),
      senderAddress: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(320)] }),
      senderName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
      smtpUsername: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(320)] }),
      sendTime: new FormControl('09:00', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(SEND_TIME_PATTERN)]
      })
    },
    { validators: [requiredWhenEnabled] }
  );

  constructor() {
    // Any edit makes the previous test result stale.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.testResult.set(null));
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.settingsService.get().subscribe({
      next: (settings) => {
        this.apply(settings);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the email settings.'));
        this.loading.set(false);
      }
    });
    this.loadDeliveries();
  }

  loadDeliveries(): void {
    this.deliveriesLoading.set(true);
    this.deliveriesError.set(null);
    this.settingsService.deliveries(20).subscribe({
      next: (deliveries) => {
        this.deliveries.set(deliveries);
        this.deliveriesLoading.set(false);
      },
      error: (err: unknown) => {
        this.deliveriesError.set(apiErrorMessage(err, 'Could not load recent deliveries.'));
        this.deliveriesLoading.set(false);
      }
    });
  }

  /** The error to show under a field: a server validation message, then the client rules. */
  fieldError(name: EmailField): string | null {
    const control = this.form.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (name === 'smtpHost' && this.form.hasError('hostRequired') && (control.touched || this.form.touched)) {
      return 'SMTP server is required when email notifications are enabled.';
    }
    if (name === 'senderAddress' && this.form.hasError('senderRequired') && (control.touched || this.form.touched)) {
      return 'Sender address is required when email notifications are enabled.';
    }
    if (!control.touched || control.valid) {
      return null;
    }
    if (control.hasError('email')) {
      return 'Enter a valid email address.';
    }
    if (control.hasError('min') || control.hasError('max') || (name === 'smtpPort' && control.hasError('required'))) {
      return 'Port must be between 1 and 65535.';
    }
    if (control.hasError('pattern') || control.hasError('required')) {
      return 'Send time must be a time in HH:mm format, for example 09:00.';
    }
    if (control.hasError('maxlength')) {
      return 'This value is too long.';
    }
    return null;
  }

  save(): void {
    this.saveError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.settingsService
      .save({
        emailEnabled: value.emailEnabled,
        smtpHost: value.smtpHost.trim(),
        smtpPort: Number(value.smtpPort),
        smtpSecurity: value.smtpSecurity,
        senderAddress: value.senderAddress.trim(),
        senderName: value.senderName.trim(),
        smtpUsername: value.smtpUsername.trim(),
        sendTime: value.sendTime
      })
      .subscribe({
        next: (settings) => {
          this.saving.set(false);
          this.apply(settings);
          this.toast.success('Email settings saved');
          this.loadDeliveries();
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.applyServerErrors(err);
          this.saveError.set(apiErrorMessage(err, 'Could not save the email settings.'));
        }
      });
  }

  /** Tests use the saved configuration, so unsaved edits block the test. */
  sendTest(): void {
    this.testResult.set(null);
    if (this.form.dirty || this.testing() || this.testAddress.invalid) {
      this.testAddress.markAsTouched();
      return;
    }

    const address = this.testAddress.value.trim();
    this.testing.set(true);
    this.settingsService.sendTest(address || null).subscribe({
      next: (result) => {
        this.testing.set(false);
        this.testResult.set(result);
        this.loadDeliveries();
      },
      error: (err: unknown) => {
        this.testing.set(false);
        this.testResult.set({
          success: false,
          message: this.firstServerError(err, 'toAddress') ?? apiErrorMessage(err, 'Could not send the test email.'),
          deliveryId: null
        });
      }
    });
  }

  statusClass(delivery: EmailDelivery): string {
    return `tf-badge email-status email-status--${delivery.status.toLowerCase()}`;
  }

  /** Server timestamps are UTC; ones without a zone suffix are read as UTC, not local time. */
  date(iso: string | null): string {
    if (!iso) {
      return '';
    }
    const utc = /[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`;
    return new Date(utc).toLocaleString();
  }

  private apply(settings: EmailSettings): void {
    this.settings.set(settings);
    this.form.reset({
      emailEnabled: settings.emailEnabled,
      smtpHost: settings.smtpHost,
      smtpPort: settings.smtpPort,
      smtpSecurity: settings.smtpSecurity,
      senderAddress: settings.senderAddress,
      senderName: settings.senderName,
      smtpUsername: settings.smtpUsername,
      sendTime: settings.sendTime
    });
  }

  private firstServerError(err: unknown, field: string): string | null {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return null;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors ?? {};
    const match = Object.entries(errors).find(([key]) => key.toLowerCase() === field.toLowerCase());
    return match?.[1]?.[0] ?? null;
  }

  /** Maps a validation ProblemDetails `errors` map (PascalCase keys) onto the matching controls. */
  private applyServerErrors(err: unknown): void {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (!errors) {
      return;
    }
    for (const [key, messages] of Object.entries(errors)) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof typeof this.form.controls;
      const control = this.form.controls[name];
      if (control && messages?.length) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
