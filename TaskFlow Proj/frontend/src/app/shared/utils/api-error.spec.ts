import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage } from './api-error';

describe('apiErrorMessage', () => {
  function httpError(status: number, error: unknown): HttpErrorResponse {
    return new HttpErrorResponse({ status, error });
  }

  it('prefers the ProblemDetails detail', () => {
    expect(apiErrorMessage(httpError(409, { detail: 'Email is already in use.', error: 'other' }), 'fallback')).toBe(
      'Email is already in use.'
    );
  });

  it('uses the { error } body when there is no detail', () => {
    expect(apiErrorMessage(httpError(400, { error: 'Username already exists.' }), 'fallback')).toBe('Username already exists.');
  });

  it('uses the { message } body returned by the task endpoints', () => {
    expect(apiErrorMessage(httpError(400, { message: 'The assigned employee is not active.' }), 'fallback')).toBe(
      'The assigned employee is not active.'
    );
  });

  it('uses the first validation message from an errors map', () => {
    const body = { title: 'One or more validation errors occurred.', errors: { Password: ['Too short.'] } };
    expect(apiErrorMessage(httpError(400, body), 'fallback')).toBe('Too short.');
  });

  it('falls back when the body has no message', () => {
    expect(apiErrorMessage(httpError(0, null), 'Could not save.')).toBe('Could not save.');
  });
});
