import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LOGIN_REASON_CREDENTIALS_CHANGED } from '../../core/auth/login-reasons';
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

const MIN_PASSWORD_LENGTH = 6;
/** Mirrors the server rule: login rejects passwords longer than this, so the change form must too. */
const MAX_PASSWORD_LENGTH = 128;

/** Mirrors the server rule for matching password confirmation. */
function passwordsMatch(group: AbstractControl): { mismatch: true } | null {
  const { newPassword, confirmPassword } = (group as FormGroup).getRawValue() as { newPassword: string; confirmPassword: string };
  return newPassword === confirmPassword ? null : { mismatch: true };
}

type ProfileField = 'fullName' | 'email' | 'mobileNumber';
type PasswordField = 'currentPassword' | 'newPassword' | 'confirmPassword' | 'mfaCode';
export type RevealField = 'current' | 'new' | 'confirm';

/** How long typed passwords stay viewable after a code unlocked them. */
export const REVEAL_UNLOCK_MINUTES = 5;
export type ProfileTab = 'profile' | 'security';

/** A rough 0-4 score so the user sees how strong a new password is. Only a hint: the API enforces the real rules. */
export function passwordStrength(password: string): { level: number; label: string } {
  if (!password) {
    return { level: 0, label: '' };
  }

  let points = 0;
  if (password.length >= MIN_PASSWORD_LENGTH) points++;
  if (password.length >= 10) points++;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) points++;
  if (/d/.test(password) && /[^A-Za-z0-9]/.test(password)) points++;

  const level = password.length < MIN_PASSWORD_LENGTH ? 1 : Math.max(1, Math.min(4, points));
  return { level, label: ['', 'Weak', 'Fair', 'Good', 'Strong'][level] };
}

/** User Profile (Screen 19). Every signed-in user views and maintains their own account here.
 * Role, project, username, employee code and status are read-only by design. */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, IconComponent, EmailLocalPartDirective],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile-shared.css', './profile.component.css']
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly emailSuffix = EMAIL_SUFFIX;
  readonly mobileHint = MOBILE_NUMBER_HINT;
  readonly roleBadgeClass = roleBadgeClass;
  readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  readonly maxPasswordLength = MAX_PASSWORD_LENGTH;

  /** Which section shows on small screens; on wide screens both always show. */
  readonly tab = signal<ProfileTab>('profile');
  readonly showCurrent = signal(false);
  readonly showNew = signal(false);
  readonly showConfirm = signal(false);

  /** Showing what was typed in a password box needs a code from the authenticator app first. */
  readonly revealUnlocked = signal(false);
  readonly unlockOpen = signal(false);
  readonly unlockBusy = signal(false);
  readonly unlockError = signal<string | null>(null);
  private unlockTarget: RevealField = 'current';
  private unlockTimer: ReturnType<typeof setTimeout> | null = null;
  readonly unlockForm = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)] })
  });

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly profile = signal<User | null>(null);

  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedAt = signal<Date | null>(null);

  readonly changingPassword = signal(false);
  readonly passwordError = signal<string | null>(null);


  readonly readonlyFields = computed(() => {
    const user = this.profile();
    return user
      ? [
          { label: 'Username', value: user.username },
          { label: 'Employee code', value: user.employeeCode || '—' },
          { label: 'Role', value: user.role },
          { label: 'Project', value: user.projectName || '—' },
          { label: 'Manager', value: user.managerName || '—' },
          ...(user.role === 'Manager'
            ? [
                { label: 'Managed projects', value: (user.managerProjects ?? []).map((d) => d.name).join(', ') || '—' },
                { label: 'Team', value: (user.reports ?? []).map((r) => r.fullName).join(', ') || '—' }
              ]
            : []),
          { label: 'Account status', value: user.isActive ? 'Active' : 'Inactive' }
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

  readonly passwordForm = new FormGroup(
    {
      currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH), Validators.maxLength(MAX_PASSWORD_LENGTH)]
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      // A fresh code from the authenticator app (or a backup code); required while two-step verification is on.
      mfaCode: new FormControl('', { nonNullable: true })
    },
    { validators: passwordsMatch }
  );

  /** What the new-password box currently holds, for the strength meter. */
  private readonly newPasswordValue = signal('');
  readonly strength = computed(() => passwordStrength(this.newPasswordValue()));

  /** The initials shown in the avatar. */
  readonly avatar = computed(() => initials(this.profile()?.fullName));

  constructor() {
    // A later edit makes the "Saved" confirmation stale.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.savedAt.set(null));
    this.passwordForm.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((value) => this.newPasswordValue.set(value));
    this.destroyRef.onDestroy(() => this.clearRelockTimer());
  }

  setTab(tab: ProfileTab): void {
    this.tab.set(tab);
  }

  /** Whether the password card needs an authenticator code (it always does while two-step verification is on). */
  readonly mfaOn = computed(() => this.profile()?.mfaEnabled === true);

  /** The eye button: hide at once; show only after an authenticator code has unlocked viewing. */
  toggleReveal(field: RevealField): void {
    const visible = this.revealSignal(field);
    if (visible()) {
      visible.set(false);
      return;
    }

    if (!this.mfaOn() || this.revealUnlocked()) {
      visible.set(true);
      return;
    }

    this.unlockTarget = field;
    this.unlockError.set(null);
    this.unlockForm.reset();
    this.unlockOpen.set(true);
  }

  verifyUnlock(): void {
    if (this.unlockForm.invalid) {
      this.unlockForm.markAllAsTouched();
      return;
    }

    if (this.unlockBusy()) {
      return;
    }

    this.unlockBusy.set(true);
    this.unlockError.set(null);
    this.profileService.checkMfaCode(this.unlockForm.controls.code.value).subscribe({
      next: () => {
        this.unlockBusy.set(false);
        this.unlockOpen.set(false);
        this.revealUnlocked.set(true);
        this.revealSignal(this.unlockTarget).set(true);
        this.unlockForm.reset();
        this.startRelockTimer();
      },
      error: (err: unknown) => {
        this.unlockBusy.set(false);
        this.unlockForm.controls.code.reset('');
        this.unlockError.set(apiErrorMessage(err, 'That code did not work. Try the new code from your app.'));
      }
    });
  }

  cancelUnlock(): void {
    this.unlockOpen.set(false);
    this.unlockError.set(null);
    this.unlockForm.reset();
  }

  /** Hides every password again and asks for a code the next time one is shown. */
  lockReveal(): void {
    this.showCurrent.set(false);
    this.showNew.set(false);
    this.showConfirm.set(false);
    this.revealUnlocked.set(false);
    this.clearRelockTimer();
  }

  private revealSignal(field: RevealField) {
    return field === 'current' ? this.showCurrent : field === 'new' ? this.showNew : this.showConfirm;
  }

  private startRelockTimer(): void {
    this.clearRelockTimer();
    this.unlockTimer = setTimeout(() => this.lockReveal(), REVEAL_UNLOCK_MINUTES * 60_000);
  }

  private clearRelockTimer(): void {
    if (this.unlockTimer !== null) {
      clearTimeout(this.unlockTimer);
      this.unlockTimer = null;
    }
  }

  private setMfaCodeRequired(required: boolean): void {
    const control = this.passwordForm.controls.mfaCode;
    control.setValidators(required ? [Validators.required] : []);
    control.updateValueAndValidity();
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
        this.setMfaCodeRequired(!!user.mfaEnabled);
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

  passwordFieldError(name: PasswordField): string | null {
    const control = this.passwordForm.controls[name];
    const server = control.getError('server') as string | undefined;
    if (server) {
      return server;
    }
    // A field only complains once the user has typed in it or left it (or tried to submit), so an untouched form
    // is not covered in errors and a mismatch shows up while the confirmation is being typed.
    if (!control.touched && !control.dirty) {
      return null;
    }
    if (name === 'mfaCode' && control.getError('required')) {
      return 'Enter the code from your authenticator app.';
    }
    if (control.getError('required')) {
      return 'Required.';
    }
    if (name === 'newPassword' && control.getError('minlength')) {
      return `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'newPassword' && control.getError('maxlength')) {
      return `Password must be at most ${MAX_PASSWORD_LENGTH} characters.`;
    }
    if (name === 'confirmPassword' && this.passwordForm.hasError('mismatch')) {
      return 'Passwords do not match.';
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

  changePassword(): void {
    this.passwordError.set(null);
    if (this.passwordForm.invalid) {
      // Show every problem at once on an attempted submit.
      this.passwordForm.markAllAsTouched();
      return;
    }

    if (this.changingPassword()) {
      return;
    }

    const value = this.passwordForm.getRawValue();
    this.changingPassword.set(true);
    this.profileService
      .changePassword(
        { currentPassword: value.currentPassword, newPassword: value.newPassword, mfaCode: value.mfaCode.trim() || undefined }
      )
      .subscribe({
        next: () => {
          // The API ended every session, including this one; sign in again with the new password.
          this.authService.clearSession();
          void this.router.navigate(['/login'], { queryParams: { reason: LOGIN_REASON_CREDENTIALS_CHANGED } });
        },
        error: (err: unknown) => {
          this.changingPassword.set(false);
          this.applyServerErrors(err, this.passwordForm);
          this.passwordError.set(apiErrorMessage(err, 'Could not change your password.'));
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
