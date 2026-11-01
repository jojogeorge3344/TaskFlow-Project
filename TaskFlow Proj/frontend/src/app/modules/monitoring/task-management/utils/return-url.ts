/** Where the task screen's Back button goes when the user did not arrive from a known screen. */
export const DEFAULT_RETURN_URL = '/tasks';

export interface BackTarget {
  /** In-app path with its query string, ready for `Router.navigateByUrl`. */
  url: string;
  label: string;
}

/** Accepts only in-app paths: one leading "/", no scheme, no protocol-relative or backslash tricks. */
export function safeReturnUrl(value: string | null | undefined): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\') ||/[\u0000-\u001f]/.test(value)) {
    return null;
  }
  return value;
}

/** The Back target for a `returnUrl` query value; invalid or missing values fall back to the task list. */
export function backTarget(returnUrl: string | null | undefined): BackTarget {
  const url = safeReturnUrl(returnUrl) ?? DEFAULT_RETURN_URL;
  const path = url.split(/[?#]/)[0];
  if (path.startsWith('/projects/')) {
    return { url, label: 'Back to project' };
  }
  if (path === '/board') {
    return { url, label: 'Back to board' };
  }
  return { url, label: 'Back to tasks' };
}
