import { Component, computed, input, output } from '@angular/core';
import { ChartConfiguration, ChartEvent } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { percentOf } from './chart-colors';

export interface DonutSlice {
  key: string;
  label: string;
  value: number;
  color: string;
}

@Component({
  selector: 'tf-donut-chart',
  standalone: true,
  imports: [BaseChartDirective],
  template: `
    @if (loading()) {
      <div class="chart-state tf-skeleton" role="status" aria-label="Loading chart"></div>
    } @else if (total() === 0) {
      <p class="chart-state chart-state--empty">{{ emptyText() }}</p>
    } @else {
      <div class="donut">
        <div class="donut__canvas">
          <canvas
            baseChart
            type="doughnut"
            role="img"
            [attr.aria-label]="ariaLabel()"
            [data]="chartData()"
            [options]="chartOptions()"
            (chartClick)="onChartClick($event)"
          ></canvas>
          <div class="donut__center" aria-hidden="true">
            <strong>{{ total() }}</strong>
            <span>{{ centerLabel() }}</span>
          </div>
        </div>
        <ul class="donut__legend">
          @for (slice of slices(); track slice.key) {
            <li>
              <button type="button" class="legend-button" [disabled]="slice.value === 0" (click)="sliceClick.emit(slice)">
                <i class="dot" [style.background]="slice.color"></i>
                <span class="legend-button__label">{{ slice.label }}</span>
                <b>{{ slice.value }} &middot; {{ percent(slice) }}%</b>
              </button>
            </li>
          }
        </ul>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .donut {
      display: flex;
      flex-wrap: wrap;
      gap: var(--tf-space-4);
      align-items: center;
    }
    .donut__canvas {
      position: relative;
      width: 9rem;
      height: 9rem;
    }
    .donut__center {
      position: absolute;
      inset: 0;
      display: grid;
      place-content: center;
      text-align: center;
      pointer-events: none;
      line-height: 1.15;
    }
    .donut__center strong {
      font-size: 1.25rem;
    }
    .donut__center span {
      color: var(--tf-muted);
      font-size: 0.75rem;
    }
    .donut__legend {
      list-style: none;
      margin: 0;
      padding: 0;
      flex: 1;
      min-width: 10rem;
    }
    .legend-button {
      display: flex;
      align-items: center;
      gap: var(--tf-space-2);
      width: 100%;
      background: none;
      border: 0;
      border-radius: var(--tf-radius-sm);
      cursor: pointer;
      font: inherit;
      padding: 0.2rem var(--tf-space-1);
      text-align: left;
    }
    .legend-button:hover:not(:disabled) {
      background: var(--tf-primary-soft);
    }
    .legend-button:disabled {
      cursor: default;
      color: var(--tf-muted);
    }
    .legend-button__label {
      flex: 1;
    }
    .dot {
      display: inline-block;
      width: 0.7rem;
      height: 0.7rem;
      border-radius: 2px;
    }
    .chart-state {
      display: block;
      min-height: 8rem;
      margin: 0;
    }
    .chart-state--empty {
      display: grid;
      place-items: center;
      color: var(--tf-muted);
      text-align: center;
    }
  `
})
export class DonutChartComponent {
  readonly slices = input<DonutSlice[]>([]);
  readonly loading = input(false);
  readonly emptyText = input('No data for these filters.');
  readonly ariaLabel = input('Donut chart');
  readonly centerLabel = input('tasks');

  readonly sliceClick = output<DonutSlice>();

  readonly total = computed(() => this.slices().reduce((sum, slice) => sum + slice.value, 0));

  readonly chartData = computed<ChartConfiguration<'doughnut'>['data']>(() => ({
    labels: this.slices().map((slice) => slice.label),
    datasets: [
      {
        data: this.slices().map((slice) => slice.value),
        backgroundColor: this.slices().map((slice) => slice.color),
        borderWidth: 1
      }
    ]
  }));

  readonly chartOptions = computed<ChartConfiguration<'doughnut'>['options']>(() => {
    const total = this.total();
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '62%',
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: (context) => `${context.label}: ${context.parsed} (${percentOf(context.parsed, total)}%)`
          }
        }
      }
    };
  });

  percent(slice: DonutSlice): number {
    return percentOf(slice.value, this.total());
  }

  onChartClick(event: { event?: ChartEvent; active?: object[] }): void {
    const first = event.active?.[0] as { index: number } | undefined;
    const slice = first ? this.slices()[first.index] : undefined;
    if (slice && slice.value > 0) {
      this.sliceClick.emit(slice);
    }
  }
}
