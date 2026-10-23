import { allocationLabel } from '../../shared/ui/allocation-format';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { EMAIL_SUFFIX, localPartOf, localPartValidator, toEmail } from '../../core/email-domain';
import { IconComponent } from '../../core/layout/icon.component';
import { MOBILE_NUMBER_HINT, mobileNumberValidator, normalizeMobileNumber } from '../../core/mobile-number';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { EmailLocalPartDirective } from '../../shared/ui/email-local-part.directive';
import { initials } from '../../shared/ui/initials';
import { ToastService } from '../../shared/ui/toast.service';
import { roleBadgeClass } from '../employees/role-badge';

type ProfileField = 'fullName' | 'email' | 'mobileNumber';

/** A read-only account fact. More than one value renders as a bulleted list; none renders as a dash. */
interface ReadonlyField {
  label: string;
  values: string[];
  /** Shown beneath the values, e.g. the total allocation. */
  note?: string | null;
}

function single(label: string, value: string | null | undefined): ReadonlyField {
  return { label, values: value ? [value] : [] };
}

function list(label: string, values: string[]): ReadonlyField {
  return { label, values };
}

/** User Profile (Screen 19). Every signed-in user views and maintains their own account here.
 * Role, project, username, employee code and status are read-only by design. */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe, IconComponent, EmailLocalPartDirective],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile-shared.css', './profile.component.css']
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly emailSuffix = EMAIL_SUFFIX;
  readonly mobileHint = MOBILE_NUMBER_HINT;
  readonly roleBadgeClass = roleBadgeClass;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly profile = signal<User | null>(null);

  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedAt = signal<Date | null>(null);


  readonly allocationLabel = allocationLabel;

  readonly readonlyFields = computed((): ReadonlyField[] => {
    const user = this.profile();
    return user
      ? [
          single('Username', user.username),
          single('Employee code', user.employeeCode),
          single('Role', user.role),
          {
            label: 'Projects',
            values: user.projectAllocations.map((allocation) => allocationLabel(allocation, true)),
            note: user.projectAllocations.length ? `${user.totalAllocatedPercent}% allocated` : null
          },
          list('Departments', user.departments.map((d) => d.name)),
          list('Managers', user.managers.map((m) => m.fullName)),
          ...(user.role === 'Manager'
            ? [
                list('Managed projects', (user.managerProjects ?? []).map((d) => d.name)),
                list('Team', (user.reports ?? []).map((r) => r.fullName))
              ]
            : []),
          single('Account status', user.isActive ? 'Active' : 'Inactive')
        ]
      : [];
  });

  readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    email: new FormControl('', {
      nonNullable: true,
      // Only the part before the fixed domain; the suffix is shown beside the input and added on save.
      validators: [Validators.required, localPartValidator]
    }),
    // Optional contact number. Colleagues may share one: email is the only unique identity.
    mobileNumber: new FormControl('', { nonNullable: true, validators: [mobileNumberValidator] })
  });

  /** The initials shown in the avatar. */
  readonly avatar = computed(() => initials(this.profile()?.fullName));

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
    this.profileService.get().subscribe({
      next: (user) => {
        this.profile.set(user);
        this.form.reset({ fullName: user.fullName, email: localPartOf(user.email), mobileNumber: user.mobileNumber ?? '' });
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load your profile.'));
        this.loading.set(false);
      }
    });
  }


  fieldError(name: ProfileField): string | null {
    const control = this.form.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    if (control.getError('required')) {
      return `${name === 'fullName' ? 'Full name' : 'Email'} is required.`;
    }
    if (control.getError('mobileNumber')) {
      return MOBILE_NUMBER_HINT;
    }
    if (control.getError('localPart')) {
      return `Use letters, digits, dot, underscore or hyphen before ${EMAIL_SUFFIX}.`;
    }
    if (control.getError('maxlength')) {
      return 'This field is too long.';
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
    this.profileService
      .update({
        fullName: value.fullName.trim(),
        email: toEmail(value.email),
        mobileNumber: normalizeMobileNumber(value.mobileNumber)
      })
      .subscribe({
        next: (user) => {
          this.saving.set(false);
          this.profile.set(user);
          this.form.reset({ fullName: user.fullName, email: localPartOf(user.email), mobileNumber: user.mobileNumber ?? '' });
          this.savedAt.set(new Date());
          this.authService.applyProfile({
            id: user.id,
            fullName: user.fullName,
            username: user.username,
            email: user.email,
            role: user.role,
            employeeCode: user.employeeCode ?? null
          });
          this.toast.success('Profile updated');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.applyServerErrors(err, this.form);
          this.saveError.set(apiErrorMessage(err, 'Could not update your profile.'));
        }
      });
  }

  /** Maps a validation ProblemDetails `errors` map (PascalCase keys) onto the matching controls. */
  private applyServerErrors(err: unknown, form: FormGroup): void {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return;
    }
    const errors = (err.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (!errors) {
      return;
    }
    for (const [key, messages] of Object.entries(errors)) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof typeof form.controls;
      const control = form.controls[name];
      if (control && messages?.length) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
