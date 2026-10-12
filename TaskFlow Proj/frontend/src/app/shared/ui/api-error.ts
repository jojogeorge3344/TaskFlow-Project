import { HttpErrorResponse } from '@angular/common/http';

/**
 * Message from an API error response, or the fallback. Checks ProblemDetails `detail`, then `{ error }`,
 * then the first message of a validation ProblemDetails `errors` map.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const body = err instanceof HttpErrorResponse ? err.error : (err as { error?: unknown } | null)?.error;
  if (body && typeof body === 'object') {
    const { detail, error, errors } = body as { detail?: unknown; error?: unknown; errors?: unknown };
    if (typeof detail === 'string' && detail.trim()) {
      return detail;
    }
    if (typeof error === 'string' && error.trim()) {
      return error;
    }
    if (errors && typeof errors === 'object') {
      const first = Object.values(errors as Record<string, unknown>)
        .flatMap((messages) => (Array.isArray(messages) ? messages : [messages]))
        .find((message): message is string => typeof message === 'string' && message.trim() !== '');
      if (first) {
        return first;
      }
    }
  }
  return fallback;
}
