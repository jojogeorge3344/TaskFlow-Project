import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BUCKET_SERIES } from '../../utils/chart-colors';
import { provideDashboardCharts } from '../../utils/chart-providers';
import { DonutChartComponent } from './donut-chart.component';
import { DonutSlice } from './donut-chart.model';
import { KpiCardComponent } from './kpi-card.component';
import { LineChartComponent } from './line-chart.component';
import { LineSeries, PointClick } from './line-chart.model';
import { ProgressListComponent } from './progress-list.component';
import { ProgressItem } from './progress-list.model';
import { StackedBarChartComponent } from './stacked-bar-chart.component';
import { SegmentClick, StackedRow } from './stacked-bar-chart.model';

function render<T>(type: new () => T, inputs: Record<string, unknown>): ComponentFixture<T> {
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture;
}

function text(fixture: ComponentFixture<unknown>): string {
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

describe('shared charts', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideDashboardCharts()] });
  });

  describe('KpiCardComponent', () => {
    it('shows the label and value', () => {
      const fixture = render(KpiCardComponent, { label: 'Employees', value: 48 });

      expect(text(fixture)).toContain('Employees');
      expect(text(fixture)).toContain('48');
    });

    it('shows a skeleton instead of the value while loading', () => {
      const fixture = render(KpiCardComponent, { label: 'Employees', value: 48, loading: true });
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('[role="status"]')).toBeTruthy();
      expect(text(fixture)).not.toContain('48');
    });

    it('applies the tone class', () => {
      const fixture = render(KpiCardComponent, { label: 'Overdue', value: 3, tone: 'bad' });

      expect((fixture.nativeElement as HTMLElement).querySelector('.kpi--bad')).toBeTruthy();
    });
  });

  describe('StackedBarChartComponent', () => {
    const rows: StackedRow[] = [
      { key: 1, label: 'Engineering', values: [10, 4, 2, 1], percent: 59 },
      { key: null, label: 'No department', values: [0, 0, 0, 0], percent: 0 }
    ];

    it('renders a chart and a table with text values', () => {
      const fixture = render(StackedBarChartComponent, { rows, series: BUCKET_SERIES, rowHeading: 'Department' });
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('canvas')).toBeTruthy();
      expect(element.querySelector('thead')?.textContent).toContain('Department');
      expect(element.querySelector('thead')?.textContent).toContain('Overdue');
      expect(element.querySelector('tbody tr')?.textContent).toContain('Engineering');
      expect(element.querySelector('tbody tr')?.textContent).toContain('59%');
    });

    it('shows the empty message when every value is zero or there are no rows', () => {
      const none = render(StackedBarChartComponent, { rows: [], series: BUCKET_SERIES, emptyText: 'Nothing here' });
      expect(text(none)).toContain('Nothing here');
      expect((none.nativeElement as HTMLElement).querySelector('canvas')).toBeFalsy();

      const zero = render(StackedBarChartComponent, { rows: [rows[1]], series: BUCKET_SERIES, emptyText: 'Nothing here' });
      expect(text(zero)).toContain('Nothing here');
    });

    it('shows a loading placeholder instead of the chart', () => {
      const fixture = render(StackedBarChartComponent, { rows, series: BUCKET_SERIES, loading: true });
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('[role="status"]')).toBeTruthy();
      expect(element.querySelector('canvas')).toBeFalsy();
    });

    it('emits the clicked segment from a table cell', () => {
      const fixture = render(StackedBarChartComponent, { rows, series: BUCKET_SERIES });
      const clicks: SegmentClick[] = [];
      fixture.componentInstance.segmentClick.subscribe((click) => clicks.push(click));

      const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('tbody tr:first-child .cell-button');
      buttons[3].click();

      expect(clicks).toEqual([{ rowKey: 1, rowLabel: 'Engineering', seriesKey: 'Overdue', value: 1 }]);
    });

    it('emits the clicked segment from the chart', () => {
      const fixture = render(StackedBarChartComponent, { rows, series: BUCKET_SERIES });
      const clicks: SegmentClick[] = [];
      fixture.componentInstance.segmentClick.subscribe((click) => clicks.push(click));

      fixture.componentInstance.onChartClick({ active: [{ datasetIndex: 1, index: 0 }] });

      expect(clicks).toEqual([{ rowKey: 1, rowLabel: 'Engineering', seriesKey: 'InProgress', value: 4 }]);
    });

    it('does not offer a click for a zero segment', () => {
      const fixture = render(StackedBarChartComponent, { rows: [{ key: 1, label: 'A', values: [0, 3, 0, 0] }], series: BUCKET_SERIES });
      const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.cell-button');

      expect(buttons[0].disabled).toBeTrue();
      expect(buttons[1].disabled).toBeFalse();
    });

    it('describes the tooltip as series, value and percentage of the group', () => {
      const fixture = render(StackedBarChartComponent, { rows, series: BUCKET_SERIES });
      const label = fixture.componentInstance.chartOptions()?.plugins?.tooltip?.callbacks?.label as (context: unknown) => string;

      const result = label.call(undefined, { dataIndex: 0, dataset: { label: 'Overdue' }, parsed: { x: 1 } });

      expect(result).toBe('Overdue: 1 (6% of 17)');
    });
  });

  describe('DonutChartComponent', () => {
    const slices: DonutSlice[] = [
      { key: 'Completed', label: 'Completed', value: 3, color: '#15803d' },
      { key: 'Overdue', label: 'Overdue', value: 1, color: '#dc2626' },
      { key: 'Pending', label: 'Pending', value: 0, color: '#94a3b8' }
    ];

    it('shows the total and a legend with values and percentages', () => {
      const fixture = render(DonutChartComponent, { slices });
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('canvas')).toBeTruthy();
      expect(element.querySelector('.donut__center')?.textContent).toContain('4');
      expect(element.querySelector('.donut__legend')?.textContent).toContain('3 · 75%');
      expect(element.querySelector('.donut__legend')?.textContent).toContain('1 · 25%');
    });

    it('shows the empty message when there is nothing to show', () => {
      const fixture = render(DonutChartComponent, { slices: [], emptyText: 'No tasks' });

      expect(text(fixture)).toContain('No tasks');
      expect((fixture.nativeElement as HTMLElement).querySelector('canvas')).toBeFalsy();
    });

    it('shows a loading placeholder', () => {
      const fixture = render(DonutChartComponent, { slices, loading: true });

      expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).toBeTruthy();
      expect((fixture.nativeElement as HTMLElement).querySelector('canvas')).toBeFalsy();
    });

    it('emits a slice from the legend and from the chart, but not an empty one', () => {
      const fixture = render(DonutChartComponent, { slices });
      const clicked: DonutSlice[] = [];
      fixture.componentInstance.sliceClick.subscribe((slice) => clicked.push(slice));

      const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.legend-button');
      buttons[0].click();
      fixture.componentInstance.onChartClick({ active: [{ index: 1 }] });
      fixture.componentInstance.onChartClick({ active: [{ index: 2 }] });

      expect(buttons[2].disabled).toBeTrue();
      expect(clicked.map((slice) => slice.key)).toEqual(['Completed', 'Overdue']);
    });
  });

  describe('LineChartComponent', () => {
    const series: LineSeries[] = [
      { key: 'created', label: 'Created', color: '#64748b', data: [1, 0, 2] },
      { key: 'completed', label: 'Completed', color: '#15803d', data: [0, 1, 1] }
    ];

    it('renders a chart', () => {
      const fixture = render(LineChartComponent, { labels: ['a', 'b', 'c'], series });

      expect((fixture.nativeElement as HTMLElement).querySelector('canvas')).toBeTruthy();
    });

    it('shows the empty message when every point is zero', () => {
      const fixture = render(LineChartComponent, {
        labels: ['a'],
        series: [{ key: 'created', label: 'Created', color: '#000', data: [0] }],
        emptyText: 'No activity'
      });

      expect(text(fixture)).toContain('No activity');
      expect((fixture.nativeElement as HTMLElement).querySelector('canvas')).toBeFalsy();
    });

    it('shows a loading placeholder', () => {
      const fixture = render(LineChartComponent, { labels: ['a'], series, loading: true });

      expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).toBeTruthy();
    });

    it('emits the clicked point', () => {
      const fixture = render(LineChartComponent, { labels: ['a', 'b', 'c'], series });
      const clicks: PointClick[] = [];
      fixture.componentInstance.pointClick.subscribe((click) => clicks.push(click));

      fixture.componentInstance.onChartClick({ active: [{ datasetIndex: 1, index: 2 }] });
      fixture.componentInstance.onChartClick({ active: [] });

      expect(clicks).toEqual([{ seriesKey: 'completed', index: 2, label: 'c', value: 1 }]);
    });
  });

  describe('ProgressListComponent', () => {
    const items: ProgressItem[] = [
      { key: 1, label: 'Apollo', percent: 78, summary: '30 open · 4 overdue', note: 'Projected end 14 Nov' },
      { key: null, label: 'No project', percent: 0, summary: '0 open' }
    ];

    it('shows percent text, a progress bar and the note', () => {
      const fixture = render(ProgressListComponent, { items });
      const element = fixture.nativeElement as HTMLElement;
      const bar = element.querySelector('[role="progressbar"]');

      expect(text(fixture)).toContain('78% · 30 open · 4 overdue');
      expect(text(fixture)).toContain('Projected end 14 Nov');
      expect(bar?.getAttribute('aria-valuenow')).toBe('78');
    });

    it('shows the empty message with no items', () => {
      const fixture = render(ProgressListComponent, { items: [], emptyText: 'No projects' });

      expect(text(fixture)).toContain('No projects');
    });

    it('shows a loading placeholder', () => {
      const fixture = render(ProgressListComponent, { items, loading: true });

      expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).toBeTruthy();
      expect((fixture.nativeElement as HTMLElement).querySelector('[role="progressbar"]')).toBeFalsy();
    });

    it('emits the clicked item', () => {
      const fixture = render(ProgressListComponent, { items });
      const clicked: ProgressItem[] = [];
      fixture.componentInstance.itemClick.subscribe((item) => clicked.push(item));

      (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.progress-item')?.click();

      expect(clicked).toEqual([items[0]]);
    });
  });
});
