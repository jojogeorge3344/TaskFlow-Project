import { addDays, defaultFilter, detectPreset, presetRange, toQueryParams, validateRange } from './analytics-filter';

describe('analytics filter helpers', () => {
  const today = '2026-10-21';

  it('computes each preset relative to the application-timezone date', () => {
    expect(presetRange('today', today)).toEqual({ from: '2026-10-21', to: '2026-10-21' });
    expect(presetRange('last7', today)).toEqual({ from: '2026-10-15', to: '2026-10-21' });
    expect(presetRange('last30', today)).toEqual({ from: '2026-09-22', to: '2026-10-21' });
    expect(presetRange('thisMonth', today)).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    expect(presetRange('previousMonth', today)).toEqual({ from: '2026-09-01', to: '2026-09-30' });
  });

  it('handles month and year boundaries', () => {
    expect(presetRange('previousMonth', '2026-01-15')).toEqual({ from: '2025-12-01', to: '2025-12-31' });
    expect(presetRange('thisMonth', '2028-02-10')).toEqual({ from: '2028-02-01', to: '2028-02-29' });
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('defaults to the last 30 days by created date with no entity filters', () => {
    expect(defaultFilter(today)).toEqual({
      from: '2026-09-22',
      to: '2026-10-21',
      basis: 'Created',
      departmentId: null,
      projectId: null,
      employeeId: null,
      managerId: null
    });
  });

  it('detects which preset a range matches', () => {
    expect(detectPreset(presetRange('last7', today), today)).toBe('last7');
    expect(detectPreset(presetRange('previousMonth', today), today)).toBe('previousMonth');
    expect(detectPreset({ from: '2026-10-02', to: '2026-10-09' }, today)).toBe('custom');
  });

  it('rejects an end date before the start date and a missing date', () => {
    expect(validateRange('2026-10-10', '2026-10-09')).toBe('The end date cannot be before the start date.');
    expect(validateRange('', '2026-10-09')).toBeTruthy();
    expect(validateRange('2026-10-10', '2026-10-10')).toBeNull();
  });

  it('builds query params with only the chosen entity filters', () => {
    const params = toQueryParams({ ...defaultFilter(today), departmentId: 3, managerId: 7 });

    expect(params).toEqual({ from: '2026-09-22', to: '2026-10-21', basis: 'Created', departmentId: '3', managerId: '7' });
  });
});
