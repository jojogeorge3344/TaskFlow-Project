import { Injectable, signal } from '@angular/core';

export type ThemeName = 'default' | 'professional' | 'dark';

export const THEME_STORAGE_KEY = 'tf.theme';
export const THEMES: readonly { name: ThemeName; label: string }[] = [
  { name: 'default', label: 'Default' },
  { name: 'professional', label: 'Professional' },
  { name: 'dark', label: 'Dark' }
];

export function isThemeName(value: unknown): value is ThemeName {
  return THEMES.some((theme) => theme.name === value);
}

/**
 * Active colour theme. Applied as data-theme on <html> (Default has no attribute) and remembered per browser.
 * Storage can be blocked or empty, so every access is guarded and failure falls back to Default.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly themeSignal = signal<ThemeName>(readStoredTheme());

  readonly theme = this.themeSignal.asReadonly();

  constructor() {
    applyTheme(this.themeSignal());
  }

  select(theme: ThemeName): void {
    if (!isThemeName(theme)) {
      return;
    }
    this.themeSignal.set(theme);
    applyTheme(theme);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Storage is unavailable; the choice still applies for this session.
    }
  }
}

function readStoredTheme(): ThemeName {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    return isThemeName(stored) ? stored : 'default';
  } catch {
    return 'default';
  }
}

function applyTheme(theme: ThemeName): void {
  if (typeof document === 'undefined') {
    return;
  }
  const root = document.documentElement;
  if (theme === 'default') {
    root.removeAttribute('data-theme');
  } else {
    root.setAttribute('data-theme', theme);
  }
}
