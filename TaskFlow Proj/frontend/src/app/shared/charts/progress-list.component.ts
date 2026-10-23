import { Component, input, output } from '@angular/core';

export interface ProgressItem {
  key: number | string | null;
  label: string;
  percent: number;
  summary: string;
  note?: string;
}

@Component({
  selector: 'tf-progress-list',
  standalone: true,
  template: `
    @if (loading()) {
      <div class="progress-state tf-skeleton" role="status" aria-label="Loading progress"></div>
    } @else if (items().length === 0) {
      <p class="progress-state progress-state--empty">{{ emptyText() }}</p>
    } @else {
      <ul class="progress-list">
        @for (item of items(); track $index) {
          <li>
            <button type="button" class="progress-item" (click)="itemClick.emit(item)">
              <span class="progress-item__head">
                <b>{{ item.label }}</b>
                <span>{{ item.percent }}% &middot; {{ item.summary }}</span>
              </span>
              <span
                class="progress-item__track"
                role="progressbar"
                aria-valuemin="0"
                aria-valuemax="100"
                [attr.aria-valuenow]="item.percent"
                [attr.aria-label]="item.label + ' ' + item.percent + '% complete'"
              >
                <span class="progress-item__fill" [style.width.%]="item.percent"></span>
              </span>
              @if (item.note) {
                <small>{{ item.note }}</small>
              }
            </button>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .progress-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .progress-item {
      display: block;
      width: 100%;
      background: none;
      border: 0;
      border-radius: var(--tf-radius-sm);
      cursor: pointer;
      font: inherit;
      padding: var(--tf-space-2) var(--tf-space-1);
      text-align: left;
    }
    .progress-item:hover {
      background: var(--tf-primary-soft);
    }
    .progress-item__head {
      display: flex;
      justify-content: space-between;
      gap: var(--tf-space-2);
      flex-wrap: wrap;
      font-size: 0.875rem;
    }
    .progress-item__track {
      display: block;
      height: 0.6rem;
      margin: var(--tf-space-1) 0;
      background: var(--tf-border);
      border-radius: 99px;
      overflow: hidden;
    }
    .progress-item__fill {
      display: block;
      height: 100%;
      background: var(--tf-success);
    }
    small {
      color: var(--tf-muted);
    }
    .progress-state {
      display: block;
      min-height: 6rem;
      margin: 0;
    }
    .progress-state--empty {
      display: grid;
      place-items: center;
      color: var(--tf-muted);
      text-align: center;
    }
  `
})
export class ProgressListComponent {
  readonly items = input<ProgressItem[]>([]);
  readonly loading = input(false);
  readonly emptyText = input('Nothing to show for these filters.');

  readonly itemClick = output<ProgressItem>();
}
