import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../../../shared/auth/auth.service';
import { AdminAnalytics, AnalyticsFilterOptions } from '../models/analytics.model';
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
  let currentUser: ReturnType<typeof signal<unknown>>;

  beforeEach(() => {
    currentUser = signal<unknown>({ id: 1 });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { currentUser } }]
    });
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

  it('still loads analytics when the filter options fail, using the server dates as today', () => {
    service.start();
    http.expectOne(`${url}/filters`).flush('boom', { status: 500, statusText: 'Server Error' });

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.has('from')).toBeFalse();
    expect(request.request.params.has('to')).toBeFalse();
    request.flush(analytics(1));

    expect(service.options()).toBeNull();
    expect(service.data()?.tasksByStatus.total).toBe(1);
    expect(service.today()).toBe('2026-10-21');
    expect(service.filter().from).toBe('2026-09-22');
    expect(service.filter().to).toBe('2026-10-21');
  });

  it('keeps the chosen filters when the dashboard is opened again', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));
    service.update({ departmentId: 1 });
    http.expectOne((candidate) => candidate.url === url).flush(analytics(2));

    service.start();

    http.expectNone(`${url}/filters`);
    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('departmentId')).toBe('1');
    request.flush(analytics(2));
  });

  it('asks for every employee when requested and resets that on reset', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.setAllEmployees(true);
    const all = http.expectOne((candidate) => candidate.url === url);
    expect(all.request.params.get('allEmployees')).toBe('true');
    all.flush(analytics(1));

    service.reset();
    const reset = http.expectOne((candidate) => candidate.url === url);
    expect(reset.request.params.has('allEmployees')).toBeFalse();
    reset.flush(analytics(1));
  });

  it('forgets everything when the user signs out', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));
    service.update({ departmentId: 1 });
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));
    TestBed.flushEffects();

    currentUser.set(null);
    TestBed.flushEffects();

    expect(service.data()).toBeNull();
    expect(service.options()).toBeNull();
    expect(service.filter().departmentId).toBeNull();
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
