import { Component, computed, inject, input, output, signal } from '@angular/core';
import { Position } from '../../core/models';
import { PositionService } from '../../core/services/position.service';
import { apiErrorMessage } from '../ui/api-error';
import { ToastService } from '../ui/toast.service';

let nextId = 0;

/** A position field: pick one of the existing positions or type a new name, which is created for everyone to reuse. A name
 * that matches an existing position ignoring case uses that position instead of creating a second one. */
@Component({
  selector: 'app-position-combobox',
  standalone: true,
  template: `
    <input
      class="tf-input"
      type="text"
      autocomplete="off"
      maxlength="100"
      [attr.id]="inputId()"
      [attr.list]="listId"
      [attr.aria-label]="label()"
      [attr.aria-invalid]="invalid()"
      [class.is-invalid]="invalid()"
      [value]="text()"
      [disabled]="busy()"
      [attr.placeholder]="placeholder()"
      (input)="onInput($any($event.target).value)"
      (change)="commit($any($event.target).value)"
    />
    <datalist [id]="listId">
      @for (position of positions(); track position.id) {
        <option [value]="position.name"></option>
      }
    </datalist>
  `
})
export class PositionComboboxComponent {
  private readonly positionService = inject(PositionService);
  private readonly toast = inject(ToastService);

  readonly positions = input<Position[]>([]);
  /** The chosen position's id, or null when none is chosen yet. */
  readonly positionId = input<number | null>(null);
  readonly inputId = input<string | null>(null);
  readonly label = input('Position');
  readonly invalid = input(false);
  readonly placeholder = input('Pick or type a position');
  /** Emits the chosen position, or null when the field is cleared. */
  readonly chosen = output<Position | null>();
  /** Emits a position this field just created, so the parent can add it to the shared list. */
  readonly created = output<Position>();

  readonly listId = `position-options-${nextId++}`;
  readonly busy = signal(false);
  private readonly typed = signal<string | null>(null);
  readonly text = computed(() => {
    const typed = this.typed();
    if (typed !== null) {
      return typed;
    }
    return this.positions().find((position) => position.id === this.positionId())?.name ?? '';
  });

  onInput(value: string): void {
    this.typed.set(value);
  }

  commit(value: string): void {
    const name = value.trim().replace(/\s+/g, ' ');
    if (!name) {
      this.typed.set(null);
      this.chosen.emit(null);
      return;
    }

    const existing = this.positions().find((position) => position.name.toLowerCase() === name.toLowerCase());
    if (existing) {
      this.typed.set(null);
      this.chosen.emit(existing);
      return;
    }

    this.busy.set(true);
    this.positionService.getOrCreate(name).subscribe({
      next: (position) => {
        this.busy.set(false);
        this.typed.set(null);
        this.created.emit(position);
        this.chosen.emit(position);
      },
      error: (err) => {
        this.busy.set(false);
        this.toast.error(apiErrorMessage(err, 'The position could not be created.'));
      }
    });
  }
}
