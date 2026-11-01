import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Mirrors the API rule: international (E.164) format, a plus sign then 8 to 15 digits with no leading zero. */
const E164_PATTERN = /^\+[1-9][0-9]{7,14}$/;

export const MOBILE_NUMBER_HINT = 'Use international format, for example +919876543210.';

/** The number as the API stores it: spaces, dashes, dots and brackets typed around it are dropped. */
export function normalizeMobileNumber(raw: string | null | undefined): string {
  return (raw ?? '').replace(/[\s\-.()]/g, '');
}

export function isValidMobileNumber(raw: string | null | undefined): boolean {
  return E164_PATTERN.test(normalizeMobileNumber(raw));
}

/** Valid when empty (add Validators.required for a mandatory field) or a well-formed international number. */
export function mobileNumberValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  const value = control.value ?? '';
  return value.trim() === '' || isValidMobileNumber(value) ? null : { mobileNumber: true };
}
