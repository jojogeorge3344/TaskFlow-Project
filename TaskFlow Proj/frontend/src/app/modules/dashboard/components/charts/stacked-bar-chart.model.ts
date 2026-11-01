export interface StackedRow {
  key: number | string | null;
  label: string;
  values: number[];
  percent?: number;
}

export interface SegmentClick {
  rowKey: number | string | null;
  rowLabel: string;
  seriesKey: string;
  value: number;
}
