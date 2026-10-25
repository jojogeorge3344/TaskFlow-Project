import { AnalyticsBasis, AnalyticsFilterState } from '../models';

export type RangePreset = 'today' | 'last7' | 'last30' | 'thisMonth' | 'previousMonth' | 'custom';

export const RANGE_PRESETS: readonly { key: RangePreset; label: string }[] = [
  { key: 'today', label: 'Today' },
  { key: 'last7', label: 'Last 7 days' },
  { key: 'last30', label: 'Last 30 days' },
  { key: 'thisMonth', label: 'This month' },
  { key: 'previousMonth', label: 'Previous month' },
  { key: 'custom', label: 'Custom range' }
];

export const BASIS_LABELS: Record<AnalyticsBasis, string> = {
  Created: 'Created date',
  Deadline: 'Deadline',
  Completed: 'Completed date'
};

export const DEFAULT_PRESET: RangePreset = 'last30';
export const DEFAULT_BASIS: AnalyticsBasis = 'Created';

const DAY_MS = 24 * 60 * 60 * 1000;

function parse(day: string): Date {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, date));
}

function format(value: Date): string {
  return value.toISOString().slice(0, 10);
}

export function addDays(day: string, days: number): string {
  return format(new Date(parse(day).getTime() + days * DAY_MS));
}

export function localToday(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const date = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${date}`;
}

export function presetRange(preset: Exclude<RangePreset, 'custom'>, today: string): { from: string; to: string } {
  const current = parse(today);
  const firstOfMonth = new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth(), 1));
  switch (preset) {
    case 'today':
      return { from: today, to: today };
    case 'last7':
      return { from: addDays(today, -6), to: today };
    case 'last30':
      return { from: addDays(today, -29), to: today };
    case 'thisMonth':
      return {
        from: format(firstOfMonth),
        to: format(new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth() + 1, 0)))
      };
    case 'previousMonth':
      return {
        from: format(new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth() - 1, 1))),
        to: format(new Date(firstOfMonth.getTime() - DAY_MS))
      };
  }
}

export function defaultFilter(today: string): AnalyticsFilterState {
  return {
    ...presetRange('last30', today),
    basis: DEFAULT_BASIS,
    departmentId: null,
    projectId: null,
    employeeId: null,
    managerId: null
  };
}

export function detectPreset(filter: Pick<AnalyticsFilterState, 'from' | 'to'>, today: string): RangePreset {
  const match = RANGE_PRESETS.filter((preset) => preset.key !== 'custom').find((preset) => {
    const range = presetRange(preset.key as Exclude<RangePreset, 'custom'>, today);
    return range.from === filter.from && range.to === filter.to;
  });
  return match?.key ?? 'custom';
}

export function validateRange(from: string, to: string): string | null {
  if (!from || !to) {
    return 'Choose both a start and an end date.';
  }
  if (to < from) {
    return 'The end date cannot be before the start date.';
  }
  return null;
}

export function toQueryParams(filter: AnalyticsFilterState): Record<string, string> {
  const params: Record<string, string> = { from: filter.from, to: filter.to, basis: filter.basis };
  if (filter.departmentId !== null) params['departmentId'] = String(filter.departmentId);
  if (filter.projectId !== null) params['projectId'] = String(filter.projectId);
  if (filter.employeeId !== null) params['employeeId'] = String(filter.employeeId);
  if (filter.managerId !== null) params['managerId'] = String(filter.managerId);
  return params;
}

export function shortDate(day: string): string {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, date)).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    timeZone: 'UTC'
  });
}
