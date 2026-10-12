import { Component, ElementRef, HostListener, effect, input, output, viewChild } from '@angular/core';
import { IconComponent } from '../../core/layout/icon.component';

const FOCUSABLE_FIELD = 'input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled])';

/**
 * Right-side panel that slides in over the page. Content (body + footer) is projected.
 * Closes on the X button, a backdrop click, or Escape; focus moves to the first field on open.
 */
@Component({
  selector: 'tf-drawer',
  standalone: true,
  imports: [IconComponent],
  template: `
    @if (open()) {
      <div class="tf-backdrop" data-role="drawer-backdrop" (click)="closed.emit()"></div>
      <section class="tf-drawer" role="dialog" aria-modal="true" aria-labelledby="tf-drawer-title">
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
  private readonly content = viewChild<ElementRef<HTMLElement>>('content');

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
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }
}
