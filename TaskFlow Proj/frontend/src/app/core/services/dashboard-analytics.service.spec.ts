import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminAnalytics, AnalyticsFilterOptions } from '../models';
import { DashboardAnalyticsService } from './dashboard-analytics.service';

describe('DashboardAnalyticsService', () => {
  const url = '/api/v1/dashboard/admin/analytics';
  const options: AnalyticsFilterOptions = {
    today: '2026-10-21',
    timezone: 'Asia/Kolkata',
    departments: [{ id: 1, name: 'Engineering' }],
    projects: [],
    employees: [],
    managers: []
  };

  function analytics(total: number): AdminAnalytics {
    const buckets = { pending: total, inProgress: 0, completed: 0, overdue: 0, total, open: total, completionPercent: 0 };
    return {
      filter: { from: '2026-09-22', to: '2026-10-21', basis: 'Created', departmentId: null, projectId: null, employeeId: null, managerId: null },
      summary: { totalEmployees: 1, totalManagers: 0, totalDepartments: 1, totalProjects: 0, tasks: buckets, totalTasks: total, completionPercent: 0 },
      tasksByStatus: buckets,
      tasksByPriority: [],
      deadlineBuckets: { overdue: 0, dueToday: 0, next7Days: 0, later: 0 },
      trend: { granularity: 'day', points: [] },
      byDepartment: [],
      byEmployee: { items: [], totalEmployees: 0 },
      byManager: [],
      byProject: []
    };
  }

  let service: DashboardAnalyticsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(DashboardAnalyticsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function start(): void {
    service.start();
    http.expectOne(`${url}/filters`).flush(options);
  }

  it('loads the options first and then the default last-30-days analytics in the application timezone', () => {
    start();

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('from')).toBe('2026-09-22');
    expect(request.request.params.get('to')).toBe('2026-10-21');
    expect(request.request.params.get('basis')).toBe('Created');
    request.flush(analytics(4));

    expect(service.data()?.tasksByStatus.total).toBe(4);
    expect(service.loading()).toBeFalse();
    expect(service.options()?.today).toBe('2026-10-21');
  });

  it('still loads analytics when the filter options fail', () => {
    service.start();
    http.expectOne(`${url}/filters`).flush('boom', { status: 500, statusText: 'Server Error' });

    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    expect(service.options()).toBeNull();
    expect(service.data()?.tasksByStatus.total).toBe(1);
  });

  it('sends the chosen entity filters', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.update({ departmentId: 1, basis: 'Deadline' });

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('departmentId')).toBe('1');
    expect(request.request.params.get('basis')).toBe('Deadline');
    request.flush(analytics(2));
  });

  it('does not call the API for an invalid range and exposes the error', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.update({ from: '2026-10-10', to: '2026-10-09' });

    expect(service.rangeError()).toBe('The end date cannot be before the start date.');
    http.expectNone((candidate) => candidate.url === url);
  });

  it('never lets an older, slower response overwrite a newer one', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.update({ departmentId: 1 });
    const first = http.expectOne((candidate) => candidate.url === url);
    service.update({ departmentId: null });
    const second = http.match((candidate) => candidate.url === url);

    expect(first.cancelled).toBeTrue();
    expect(second.length).toBe(1);
    second[0].flush(analytics(20));
    expect(service.data()?.tasksByStatus.total).toBe(20);
    expect(service.loading()).toBeFalse();
  });

  it('keeps the previous data and offers a retry when a request fails', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(5));

    service.update({ projectId: 9 });
    http.expectOne((candidate) => candidate.url === url).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(service.error()).toBeTruthy();
    expect(service.data()?.tasksByStatus.total).toBe(5);

    service.refresh();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(6));
    expect(service.error()).toBeNull();
    expect(service.data()?.tasksByStatus.total).toBe(6);
  });

  it('resets to the default filters', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));
    service.update({ departmentId: 1, basis: 'Completed' });
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.reset();

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.has('departmentId')).toBeFalse();
    expect(request.request.params.get('basis')).toBe('Created');
    expect(service.filter().from).toBe('2026-09-22');
    request.flush(analytics(1));
  });
});
