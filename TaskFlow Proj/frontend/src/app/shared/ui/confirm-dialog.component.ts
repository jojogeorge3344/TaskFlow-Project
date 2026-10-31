import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { IconComponent } from '../../core/layout/icon.component';
import { ConfirmOptions } from './confirm-dialog.model';

/**
 * Confirmation dialog built on the native <dialog> (focus trap, Esc and backdrop for free).
 * Place one in a template and call open(); the promise resolves true on confirm, false otherwise.
 */
@Component({
  selector: 'tf-confirm-dialog',
  standalone: true,
  imports: [IconComponent],
  template: `
    <dialog
      #dialog
      class="tf-modal tf-modal--confirm"
      aria-labelledby="tf-confirm-title"
      aria-describedby="tf-confirm-message"
      (cancel)="onCancelEvent($event)"
      (click)="onDialogClick($event)"
    >
      @if (options(); as opts) {
        <div class="tf-modal__body">
          <span class="tf-modal__icon" [class.tf-modal__icon--danger]="opts.variant === 'danger'">
            <tf-icon name="alert" />
          </span>
          <div>
            <h2 id="tf-confirm-title">{{ opts.title }}</h2>
            <p id="tf-confirm-message">{{ opts.message }}</p>
          </div>
        </div>
        <div class="tf-modal__foot">
          <button type="button" class="tf-btn tf-btn--ghost" data-action="cancel" (click)="cancel()">Cancel</button>
          <button
            type="button"
            class="tf-btn"
            data-action="confirm"
            [class.tf-btn--danger]="opts.variant === 'danger'"
            [class.tf-btn--primary]="opts.variant !== 'danger'"
            (click)="confirm()"
          >
            {{ opts.confirmLabel }}
          </button>
        </div>
      }
    </dialog>
  `
})
export class ConfirmDialogComponent {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private resolve: ((confirmed: boolean) => void) | null = null;

  readonly options = signal<ConfirmOptions | null>(null);

  open(options: ConfirmOptions): Promise<boolean> {
    this.finish(false);
    this.options.set(options);
    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      dialog.showModal();
    }
    return new Promise<boolean>((resolve) => {
      this.resolve = resolve;
    });
  }

  confirm(): void {
    this.finish(true);
  }

  cancel(): void {
    this.finish(false);
  }

  onCancelEvent(event: Event): void {
    // Esc: close through our own path so the promise resolves.
    event.preventDefault();
    this.cancel();
  }

  onDialogClick(event: MouseEvent): void {
    // A click on the <dialog> element itself (not its content) is a click on the backdrop.
    if (event.target === this.dialog().nativeElement) {
      this.cancel();
    }
  }

  private finish(confirmed: boolean): void {
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      dialog.close();
    }
    const resolve = this.resolve;
    this.resolve = null;
    resolve?.(confirmed);
  }
}
