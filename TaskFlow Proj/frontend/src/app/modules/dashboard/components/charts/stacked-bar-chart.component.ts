import { Component, computed, input, output } from '@angular/core';
import { ChartConfiguration, ChartEvent } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ChartSeries, percentOf } from '../../utils/chart-colors';
import { StackedRow, SegmentClick } from './stacked-bar-chart.model';

const ROW_HEIGHT = 34;
const CHART_PADDING = 76;

@Component({
  selector: 'tf-stacked-bar-chart',
  standalone: true,
  imports: [BaseChartDirective],
  template: `
    @if (loading()) {
      <div class="chart-state tf-skeleton" role="status" aria-label="Loading chart"></div>
    } @else if (!hasData()) {
      <p class="chart-state chart-state--empty">{{ emptyText() }}</p>
    } @else {
      <div class="chart-box" [style.height.px]="height()">
        <canvas
          baseChart
          type="bar"
          role="img"
          [attr.aria-label]="ariaLabel()"
          [data]="chartData()"
          [options]="chartOptions()"
          (chartClick)="onChartClick($event)"
        ></canvas>
      </div>
      @if (showTable()) {
        <div class="analytics-table-wrap">
          <table class="analytics-table chart-table">
            <caption class="tf-sr-only">{{ ariaLabel() }}</caption>
            <thead>
              <tr>
                <th scope="col">{{ rowHeading() }}</th>
                @for (item of series(); track item.key) {
                  <th scope="col" class="num">{{ item.label }}</th>
                }
                <th scope="col" class="num">Total</th>
                @if (hasPercent()) {
                  <th scope="col" class="num">Done</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track $index) {
                <tr>
                  <th scope="row">{{ row.label }}</th>
                  @for (item of series(); track item.key; let index = $index) {
                    <td class="num">
                      <button
                        type="button"
                        class="cell-button"
                        [disabled]="row.values[index] === 0"
                        [attr.aria-label]="row.label + ' ' + item.label + ': ' + row.values[index]"
                        (click)="emit(row, index)"
                      >
                        {{ row.values[index] }}
                      </button>
                    </td>
                  }
                  <td class="num">{{ total(row) }}</td>
                  @if (hasPercent()) {
                    <td class="num">{{ row.percent ?? 0 }}%</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .chart-box {
      position: relative;
      width: 100%;
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
    .chart-table {
      margin-top: var(--tf-space-3);
    }
    .num {
      text-align: right;
    }
    .cell-button {
      background: none;
      border: 0;
      color: var(--tf-primary);
      cursor: pointer;
      font: inherit;
      padding: 0;
    }
    .cell-button:disabled {
      color: var(--tf-muted);
      cursor: default;
    }
  `
})
export class StackedBarChartComponent {
  readonly rows = input<StackedRow[]>([]);
  readonly series = input<readonly ChartSeries[]>([]);
  readonly loading = input(false);
  readonly emptyText = input('No data for these filters.');
  readonly ariaLabel = input('Stacked bar chart');
  readonly rowHeading = input('Name');
  readonly showTable = input(true);

  readonly segmentClick = output<SegmentClick>();

  readonly hasData = computed(() => this.rows().some((row) => row.values.some((value) => value > 0)));
  readonly hasPercent = computed(() => this.rows().some((row) => row.percent !== undefined));
  readonly height = computed(() => this.rows().length * ROW_HEIGHT + CHART_PADDING);

  readonly chartData = computed<ChartConfiguration<'bar'>['data']>(() => ({
    labels: this.rows().map((row) => row.label),
    datasets: this.series().map((item, index) => ({
      label: item.label,
      data: this.rows().map((row) => row.values[index] ?? 0),
      backgroundColor: item.color,
      borderWidth: 0
    }))
  }));

  readonly chartOptions = computed<ChartConfiguration<'bar'>['options']>(() => {
    const rows = this.rows();
    return {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: { stacked: true, beginAtZero: true, ticks: { precision: 0 } },
        y: { stacked: true }
      },
      plugins: {
        legend: { position: 'bottom', labels: { boxWidth: 12 } },
        tooltip: {
          callbacks: {
            label: (context) => {
              const row = rows[context.dataIndex];
              const value = context.parsed.x ?? 0;
              const rowTotal = row ? this.total(row) : 0;
              return `${context.dataset.label}: ${value} (${percentOf(value, rowTotal)}% of ${rowTotal})`;
            }
          }
        }
      }
    };
  });

  total(row: StackedRow): number {
    return row.values.reduce((sum, value) => sum + value, 0);
  }

  emit(row: StackedRow, seriesIndex: number): void {
    const item = this.series()[seriesIndex];
    if (!item) {
      return;
    }
    this.segmentClick.emit({
      rowKey: row.key,
      rowLabel: row.label,
      seriesKey: item.key,
      value: row.values[seriesIndex] ?? 0
    });
  }

  onChartClick(event: { event?: ChartEvent; active?: object[] }): void {
    const first = event.active?.[0] as { datasetIndex: number; index: number } | undefined;
    const row = first ? this.rows()[first.index] : undefined;
    if (first && row) {
      this.emit(row, first.datasetIndex);
    }
  }
}
