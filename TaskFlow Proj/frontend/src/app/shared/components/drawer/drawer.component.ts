import { Component, ElementRef, HostListener, effect, input, output, viewChild } from '@angular/core';
import { IconComponent } from '../icon/icon.component';

const FOCUSABLE_FIELD = 'input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled])';
const FOCUSABLE = `${FOCUSABLE_FIELD}, a[href], [tabindex]:not([tabindex="-1"])`;

/**
 * Right-side panel that slides in over the page. Content (body + footer) is projected.
 * Closes on the X button, a backdrop click, or Escape. While open, focus moves to the first field
 * and Tab stays inside the panel; on close, focus returns to the control that opened it.
 */
@Component({
  selector: 'tf-drawer',
  standalone: true,
  imports: [IconComponent],
  template: `
    @if (open()) {
      <div class="tf-backdrop" data-role="drawer-backdrop" (click)="closed.emit()"></div>
      <section
        #panel
        class="tf-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="tf-drawer-title"
        (keydown.tab)="trapFocus($any($event))"
        (keydown.shift.tab)="trapFocus($any($event))"
      >
        <header class="tf-drawer__head">
          <h2 id="tf-drawer-title">{{ title() }}</h2>
          <button type="button" class="tf-btn tf-btn--icon" aria-label="Close panel" (click)="closed.emit()">
            <tf-icon name="x" />
          </button>
        </header>
        <div #content class="tf-drawer__content">
          <ng-content />
        </div>
      </section>
    }
  `
})
export class DrawerComponent {
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private readonly content = viewChild<ElementRef<HTMLElement>>('content');
  private returnFocusTo: HTMLElement | null = null;

  readonly open = input(false);
  readonly title = input('');
  readonly closed = output<void>();

  constructor() {
    effect(() => {
      const content = this.content();
      // Reading title() re-runs this when the panel switches (e.g. details -> edit) while staying open.
      this.title();
      if (this.open() && content) {
        // Wait for the projected panel to render before moving focus into it.
        setTimeout(() => content.nativeElement.querySelector<HTMLElement>(FOCUSABLE_FIELD)?.focus());
      }
    });

    effect(() => {
      if (this.open()) {
        // Remember the opener once per opening (not when the panel switches while open).
        if (this.returnFocusTo === null && document.activeElement instanceof HTMLElement) {
          this.returnFocusTo = document.activeElement;
        }
      } else if (this.returnFocusTo) {
        const opener = this.returnFocusTo;
        this.returnFocusTo = null;
        setTimeout(() => {
          if (opener.isConnected) {
            opener.focus();
          }
        });
      }
    });
  }

  /** Keeps Tab / Shift+Tab cycling within the open panel. */
  trapFocus(event: KeyboardEvent): void {
    const panel = this.panel()?.nativeElement;
    if (!panel) {
      return;
    }
    const focusable = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
      (element) => element.offsetParent !== null || element === document.activeElement
    );
    if (focusable.length === 0) {
      event.preventDefault();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }
}
