export interface ChartSeries {
  key: string;
  label: string;
  color: string;
}

export const COLOR_COMPLETED = '#15803d';
export const COLOR_IN_PROGRESS = '#2563eb';
export const COLOR_PENDING = '#94a3b8';
export const COLOR_OVERDUE = '#dc2626';

export const BUCKET_SERIES: readonly ChartSeries[] = [
  { key: 'Completed', label: 'Completed', color: COLOR_COMPLETED },
  { key: 'InProgress', label: 'In progress', color: COLOR_IN_PROGRESS },
  { key: 'Pending', label: 'Pending', color: COLOR_PENDING },
  { key: 'Overdue', label: 'Overdue', color: COLOR_OVERDUE }
];

export const PRIORITY_COLORS: Record<string, string> = {
  Critical: '#7f1d1d',
  High: '#dc2626',
  Medium: '#d97706',
  Low: '#64748b'
};

export function percentOf(value: number, total: number): number {
  return total <= 0 ? 0 : Math.round((value / total) * 100);
}

export function bucketValues(counts: { completed: number; inProgress: number; pending: number; overdue: number }): number[] {
  return [counts.completed, counts.inProgress, counts.pending, counts.overdue];
}
