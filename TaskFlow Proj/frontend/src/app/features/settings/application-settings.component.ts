import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { ApplicationSettings, TASK_PRIORITIES, TaskPriority } from '../../core/models';
import { ApplicationSettingsService } from '../../core/services/application-settings.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

type SettingsField = 'name' | 'timezone' | 'defaultPriority';

/** Required, ignoring surrounding whitespace — mirrors the server rule. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Application Settings (Screen 20). Administrator only; validation mirrors
 * UpdateApplicationSettingsRequestValidator. Reminder intervals and mail delivery stay owned by
 * the Reminder Settings screen (13), which this screen links to instead of duplicating. */
@Component({
  selector: 'app-application-settings',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconComponent],
  templateUrl: './application-settings.component.html',
  styleUrl: './application-settings.component.css'
})
export class ApplicationSettingsComponent implements OnInit {
  private readonly settingsService = inject(ApplicationSettingsService);
  private readonly toast = inject(ToastService);

  readonly priorities: TaskPriority[] = TASK_PRIORITIES;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedAt = signal<Date | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [requiredText, Validators.maxLength(100)] }),
    timezone: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    defaultPriority: new FormControl<TaskPriority>('Medium', { nonNullable: true, validators: [Validators.required] }),
    inAppNotificationsEnabled: new FormControl(true, { nonNullable: true })
  });

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
    this.settingsService.get().subscribe({
      next: (settings) => {
        this.apply(settings);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the application settings.'));
        this.loading.set(false);
      }
    });
  }

  /** The error to show under a field: a server validation message, or the client-side rule. */
  fieldError(name: SettingsField): string | null {
    const control = this.form.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (name === 'name' && (control.getError('required') || control.getError('maxlength'))) {
      return 'Application name is required and must be 100 characters or fewer.';
    }
    if (name === 'timezone' && control.getError('required')) {
      return 'Timezone is required.';
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
    this.settingsService
      .save({
        name: value.name.trim(),
        timezone: value.timezone.trim(),
        defaultPriority: value.defaultPriority,
        inAppNotificationsEnabled: value.inAppNotificationsEnabled
      })
      .subscribe({
        next: (settings) => {
          this.saving.set(false);
          this.apply(settings);
          this.savedAt.set(new Date());
          this.toast.success('Application settings saved');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.applyServerErrors(err);
          this.saveError.set(apiErrorMessage(err, 'Could not save the application settings.'));
        }
      });
  }

  private apply(settings: ApplicationSettings): void {
    this.form.reset({
      name: settings.name,
      timezone: settings.timezone,
      defaultPriority: settings.defaultPriority,
      inAppNotificationsEnabled: settings.inAppNotificationsEnabled
    });
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
