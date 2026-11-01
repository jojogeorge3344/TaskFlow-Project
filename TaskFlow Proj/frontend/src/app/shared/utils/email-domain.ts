import { AbstractControl, ValidationErrors } from '@angular/forms';

/** The one domain every employee email is on. The API enforces it; the screens only mirror it. */
export const EMAIL_DOMAIN = 'taskflow.com';
export const EMAIL_SUFFIX = `@${EMAIL_DOMAIN}`;
export const MAX_EMAIL_LOCAL_PART_LENGTH = 64;

const LOCAL_PART_PATTERN = /^[a-z0-9_-]+(\.[a-z0-9_-]+)*$/;

/** What the user typed, reduced to a local part: everything from the first `@` is dropped, then trimmed and lowercased. */
export function sanitizeLocalPart(raw: string): string {
  const at = raw.indexOf('@');
  return (at >= 0 ? raw.slice(0, at) : raw).replace(/\s+/g, '').toLowerCase();
}

/** The part of a stored email before the `@`. */
export function localPartOf(email: string | null | undefined): string {
  return sanitizeLocalPart(email ?? '');
}

/** The address sent to the API for a local part. */
export function toEmail(localPart: string): string {
  return `${sanitizeLocalPart(localPart)}${EMAIL_SUFFIX}`;
}

/** Valid when the local part uses only letters, digits, dot, underscore and hyphen, without leading, trailing or doubled dots. */
export function localPartValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  if (value === '') {
    return null;
  }
  return value.length <= MAX_EMAIL_LOCAL_PART_LENGTH && LOCAL_PART_PATTERN.test(value) ? null : { localPart: true };
}
