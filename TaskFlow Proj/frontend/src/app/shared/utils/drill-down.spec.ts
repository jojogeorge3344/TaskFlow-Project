import { convertToParamMap } from '@angular/router';
import { AnalyticsFilterState } from '../../modules/dashboard/analytics/models/analytics.model';
import { defaultFilter } from './analytics-filter';
import { drillDownQuery, employeeDrillQuery, parseDrillDown } from './drill-down';

describe('drill-down mapping', () => {
  const filter: AnalyticsFilterState = { ...defaultFilter('2026-10-21'), departmentId: 2 };

  it('carries the dashboard filters and the clicked bucket', () => {
    const params = drillDownQuery(filter, { bucket: 'Overdue', departmentId: 7 });

    expect(params).toEqual({
      from: '2026-09-22',
      to: '2026-10-21',
      basis: 'Created',
      departmentId: '7',
      bucket: 'Overdue'
    });
  });

  it('counts every task of a group when no bucket is clicked', () => {
    expect(drillDownQuery(filter, { employeeId: 5 })['bucket']).toBe('All');
  });

  it('maps the "No department" and "No project" groups to id 0', () => {
    const params = drillDownQuery(defaultFilter('2026-10-21'), { departmentId: null, projectId: null, bucket: 'Pending' });

    expect(params['departmentId']).toBe('0');
    expect(params['projectId']).toBe('0');
  });

  it('opens the clicked period for a trend point and keeps the other filters', () => {
    const params = drillDownQuery(filter, { range: { from: '2026-10-10', to: '2026-10-10', basis: 'Completed' } });

    expect(params['from']).toBe('2026-10-10');
    expect(params['to']).toBe('2026-10-10');
    expect(params['basis']).toBe('Completed');
    expect(params['departmentId']).toBe('2');
    expect(params['bucket']).toBe('All');
  });

  it('uses the deadline bucket instead of a status bucket for the deadlines chart', () => {
    const params = drillDownQuery(filter, { deadlineBucket: 'DueToday' });

    expect(params['deadlineBucket']).toBe('DueToday');
    expect(params['bucket']).toBeUndefined();
  });

  it('round-trips into a task list request', () => {
    const params = drillDownQuery(filter, { bucket: 'InProgress', priority: 'High', managerId: 9 });

    const parsed = parseDrillDown(convertToParamMap(params));

    expect(parsed?.request).toEqual({
      bucket: 'InProgress',
      priority: 'High',
      departmentId: 2,
      managerId: 9,
      from: '2026-09-22',
      to: '2026-10-21',
      basis: 'Created'
    });
    expect(parsed?.summary).toContain('In progress');
  });

  it('returns null when there is nothing usable', () => {
    expect(parseDrillDown(convertToParamMap({}))).toBeNull();
    expect(parseDrillDown(convertToParamMap({ bucket: 'bad', departmentId: '-1', from: '2026-01-01' }))).toBeNull();
  });

  describe('employeeDrillQuery', () => {
    it('always names the caller as the employee and adds no date range by default', () => {
      expect(employeeDrillQuery(3, { bucket: 'Overdue' })).toEqual({ employeeId: '3', bucket: 'Overdue' });
    });

    it('maps a project, priority, deadline row and day range', () => {
      expect(employeeDrillQuery(3, { projectId: 7, bucket: 'All' })).toEqual({ employeeId: '3', projectId: '7', bucket: 'All' });
      expect(employeeDrillQuery(3, { priority: 'High', bucket: 'All' })['priority']).toBe('High');
      expect(employeeDrillQuery(3, { deadlineBucket: 'Next7Days' })['deadlineBucket']).toBe('Next7Days');
      expect(
        employeeDrillQuery(3, { bucket: 'All', range: { from: '2026-10-21', to: '2026-10-21', basis: 'Completed' } })
      ).toEqual({ employeeId: '3', from: '2026-10-21', to: '2026-10-21', basis: 'Completed', bucket: 'All' });
    });
  });
});
