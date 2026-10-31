import { Injectable, computed, signal } from '@angular/core';

export const SIDEBAR_COLLAPSED_KEY = 'tf.sidebar.collapsed';
export const WIDE_VIEWPORT_QUERY = '(min-width: 992px)';

/**
 * Side menu state. On wide viewports the menu collapses to an icon rail (remembered per browser);
 * on narrow viewports it opens as an off-canvas drawer (never remembered).
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly collapsedSignal = signal(readCollapsed());
  private readonly mobileOpenSignal = signal(false);
  private readonly isWideSignal = signal(true);

  readonly collapsed = this.collapsedSignal.asReadonly();
  readonly mobileOpen = this.mobileOpenSignal.asReadonly();
  readonly isWide = this.isWideSignal.asReadonly();

  /** Whether the menu is currently showing its labels: expanded on wide screens, open on narrow ones. */
  readonly expanded = computed(() => (this.isWide() ? !this.collapsed() : this.mobileOpen()));

  constructor() {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return;
    }

    const query = window.matchMedia(WIDE_VIEWPORT_QUERY);
    this.setWide(query.matches);
    query.addEventListener('change', (event) => this.setWide(event.matches));
  }

  toggle(): void {
    if (this.isWide()) {
      this.toggleCollapsed();
    } else {
      this.mobileOpenSignal.update((open) => !open);
    }
  }

  toggleCollapsed(): void {
    const next = !this.collapsedSignal();
    this.collapsedSignal.set(next);
    writeCollapsed(next);
  }

  closeMobile(): void {
    this.mobileOpenSignal.set(false);
  }

  private setWide(wide: boolean): void {
    this.isWideSignal.set(wide);
    if (wide) {
      this.mobileOpenSignal.set(false);
    }
  }
}

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeCollapsed(collapsed: boolean): void {
  try {
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(collapsed));
  } catch {
    // Storage blocked (e.g. private mode): the state simply is not remembered.
  }
}
