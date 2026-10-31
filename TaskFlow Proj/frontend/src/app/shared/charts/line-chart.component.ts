import { Component, computed, input, output } from '@angular/core';
import { ChartConfiguration, ChartEvent } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ChartSeries } from './chart-colors';
import { LineSeries, PointClick } from './line-chart.model';

@Component({
  selector: 'tf-line-chart',
  standalone: true,
  imports: [BaseChartDirective],
  template: `
    @if (loading()) {
      <div class="chart-state tf-skeleton" role="status" aria-label="Loading chart"></div>
    } @else if (!hasData()) {
      <p class="chart-state chart-state--empty">{{ emptyText() }}</p>
    } @else {
      <div class="chart-box">
        <canvas
          baseChart
          type="line"
          role="img"
          [attr.aria-label]="ariaLabel()"
          [data]="chartData()"
          [options]="chartOptions()"
          (chartClick)="onChartClick($event)"
        ></canvas>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .chart-box {
      position: relative;
      width: 100%;
      height: 16rem;
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
export class LineChartComponent {
  readonly labels = input<string[]>([]);
  readonly series = input<LineSeries[]>([]);
  readonly loading = input(false);
  readonly emptyText = input('No data for these filters.');
  readonly ariaLabel = input('Line chart');

  readonly pointClick = output<PointClick>();

  readonly hasData = computed(() => this.series().some((item) => item.data.some((value) => value > 0)));

  readonly chartData = computed<ChartConfiguration<'line'>['data']>(() => ({
    labels: this.labels(),
    datasets: this.series().map((item) => ({
      label: item.label,
      data: item.data,
      borderColor: item.color,
      backgroundColor: item.color,
      borderDash: item.dashed ? [5, 3] : [],
      pointRadius: 3,
      pointHoverRadius: 6,
      tension: 0.2
    }))
  }));

  readonly chartOptions = computed<ChartConfiguration<'line'>['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'nearest', intersect: true },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
    plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } }
  }));

  onChartClick(event: { event?: ChartEvent; active?: object[] }): void {
    const first = event.active?.[0] as { datasetIndex: number; index: number } | undefined;
    const item = first ? this.series()[first.datasetIndex] : undefined;
    if (first && item) {
      this.pointClick.emit({
        seriesKey: item.key,
        index: first.index,
        label: this.labels()[first.index] ?? '',
        value: item.data[first.index] ?? 0
      });
    }
  }
}
