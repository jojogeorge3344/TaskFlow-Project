const KEY = 'taskflow.lastIdentifier';

/** The identifier last used to sign in on this device, to pre-fill the Login Screen. A per-viewer
 * convenience only: storage can be blocked or empty, so every access is guarded and failure is silent. */
export function readRememberedIdentifier(): string {
  try {
    return localStorage.getItem(KEY)?.trim() ?? '';
  } catch {
    return '';
  }
}

export function rememberIdentifier(identifier: string): void {
  try {
    localStorage.setItem(KEY, identifier.trim());
  } catch {
    // Storage is unavailable; the pre-fill is simply skipped next time.
  }
}
