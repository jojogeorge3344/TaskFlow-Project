import { ChartSeries } from './chart-colors';

export interface LineSeries extends ChartSeries {
  data: number[];
  dashed?: boolean;
}

export interface PointClick {
  seriesKey: string;
  index: number;
  label: string;
  value: number;
}
