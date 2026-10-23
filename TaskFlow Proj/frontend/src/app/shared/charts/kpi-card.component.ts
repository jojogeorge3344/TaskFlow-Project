import { Component, input } from '@angular/core';

export type KpiTone = 'neutral' | 'good' | 'bad';

@Component({
  selector: 'tf-kpi-card',
  standalone: true,
  template: `
    <article class="kpi" [class.kpi--good]="tone() === 'good'" [class.kpi--bad]="tone() === 'bad'">
      <span class="kpi__label">{{ label() }}</span>
      @if (loading()) {
        <span class="kpi__value kpi__value--loading tf-skeleton" role="status" aria-label="Loading"></span>
      } @else {
        <span class="kpi__value">{{ value() }}</span>
      }
    </article>
  `,
  styles: `
    .kpi {
      background: var(--tf-surface);
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius);
      box-shadow: var(--tf-shadow-sm);
      padding: var(--tf-space-3) var(--tf-space-4);
      display: flex;
      flex-direction: column;
      gap: var(--tf-space-1);
    }
    .kpi__label {
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }
    .kpi__value {
      font-size: 1.75rem;
      font-weight: 700;
      line-height: 1.2;
    }
    .kpi__value--loading {
      display: block;
      width: 4rem;
      height: 2rem;
    }
    .kpi--good .kpi__value {
      color: var(--tf-success);
    }
    .kpi--bad .kpi__value {
      color: var(--tf-danger);
    }
  `
})
export class KpiCardComponent {
  readonly label = input.required<string>();
  readonly value = input<number | string | null>(0);
  readonly tone = input<KpiTone>('neutral');
  readonly loading = input(false);
}
