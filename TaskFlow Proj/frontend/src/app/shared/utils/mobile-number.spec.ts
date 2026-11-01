import { FormControl } from '@angular/forms';
import { isValidMobileNumber, mobileNumberValidator, normalizeMobileNumber } from './mobile-number';

describe('mobile number helpers', () => {
  it('strips spaces, dashes, dots and brackets', () => {
    expect(normalizeMobileNumber(' +91 (98765)-43210 ')).toBe('+919876543210');
    expect(normalizeMobileNumber(null)).toBe('');
  });

  it('accepts international numbers and rejects everything else', () => {
    for (const ok of ['+919876543210', '+14155552671', '+91 98765-43210']) {
      expect(isValidMobileNumber(ok)).withContext(ok).toBeTrue();
    }
    for (const bad of ['9876543210', '+0123456789', '+12345', '+1234567890123456', '+91abc4567890', '']) {
      expect(isValidMobileNumber(bad)).withContext(bad).toBeFalse();
    }
  });

  it('the validator allows empty and flags a malformed number', () => {
    expect(mobileNumberValidator(new FormControl(''))).toBeNull();
    expect(mobileNumberValidator(new FormControl('+919876543210'))).toBeNull();
    expect(mobileNumberValidator(new FormControl('12345'))).toEqual({ mobileNumber: true });
  });
});
