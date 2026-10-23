import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { TeamAnalytics, TeamFilterOptions } from '../models';
import { ManagerAnalyticsService } from './manager-analytics.service';

describe('ManagerAnalyticsService', () => {
  const url = '/api/v1/dashboard/manager/analytics';
  const options: TeamFilterOptions = {
    today: '2026-10-21',
    timezone: 'Asia/Kolkata',
    employees: [{ id: 3, name: 'A. Thomas' }],
    projects: [{ id: 4, name: 'Apollo' }]
  };

  function analytics(total: number): TeamAnalytics {
    const buckets = { pending: total, inProgress: 0, completed: 0, overdue: 0, total, open: total, completionPercent: 0 };
    return {
      filter: { from: '2026-09-22', to: '2026-10-21', basis: 'Created', departmentId: null, projectId: null, employeeId: null, managerId: null },
      summary: { teamSize: 1, totalProjects: 0, tasks: buckets, totalTasks: total, completionPercent: 0 },
      tasksByStatus: buckets,
      tasksByPriority: [],
      byEmployee: [],
      byProject: [],
      attention: { delayedTasks: [], projectsNeedingAttention: [] }
    };
  }

  let service: ManagerAnalyticsService;
  let http: HttpTestingController;
  let currentUser: ReturnType<typeof signal<unknown>>;

  beforeEach(() => {
    currentUser = signal<unknown>({ id: 1 });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { currentUser } }]
    });
    service = TestBed.inject(ManagerAnalyticsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function start(): void {
    service.start();
    http.expectOne(`${url}/filters`).flush(options);
  }

  it('loads the scoped options first and then the default last-30-days analytics', () => {
    start();

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('from')).toBe('2026-09-22');
    expect(request.request.params.get('to')).toBe('2026-10-21');
    expect(request.request.params.get('basis')).toBe('Created');
    request.flush(analytics(4));

    expect(service.data()?.tasksByStatus.total).toBe(4);
    expect(service.loading()).toBeFalse();
    expect(service.today()).toBe('2026-10-21');
  });

  it('exposes only the in-scope employees and projects and never any departments or managers', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    expect(service.options()?.employees).toEqual([{ id: 3, name: 'A. Thomas' }]);
    expect(service.options()?.projects).toEqual([{ id: 4, name: 'Apollo' }]);
    expect(service.options()?.departments).toEqual([]);
    expect(service.options()?.managers).toEqual([]);
  });

  it('sends the employee and project filters but never a department or manager parameter', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.update({ employeeId: 3, projectId: 4 });

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('employeeId')).toBe('3');
    expect(request.request.params.get('projectId')).toBe('4');
    expect(request.request.params.has('departmentId')).toBeFalse();
    expect(request.request.params.has('managerId')).toBeFalse();
    request.flush(analytics(2));
  });

  it('still loads analytics when the filter options fail, using the server dates as today', () => {
    service.start();
    http.expectOne(`${url}/filters`).flush('boom', { status: 500, statusText: 'Server Error' });

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.has('from')).toBeFalse();
    request.flush(analytics(1));

    expect(service.options()).toBeNull();
    expect(service.today()).toBe('2026-10-21');
    expect(service.filter().from).toBe('2026-09-22');
  });

  it('keeps the previous results and does not call the server for an invalid custom range', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(7));

    service.update({ from: '2026-10-21', to: '2026-10-01' });

    http.expectNone((candidate) => candidate.url === url);
    expect(service.rangeError()).toContain('end date cannot be before');
    expect(service.data()?.tasksByStatus.total).toBe(7);
  });

  it('shows a retryable error and keeps the last data when a request fails', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(3));

    service.update({ projectId: 4 });
    http.expectOne((candidate) => candidate.url === url).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(service.error()).toContain('Could not load analytics');
    expect(service.loading()).toBeFalse();

    service.refresh();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(5));
    expect(service.error()).toBeNull();
    expect(service.data()?.tasksByStatus.total).toBe(5);
  });

  it('forgets everything when the user signs out', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(2));
    TestBed.flushEffects();

    currentUser.set(null);
    TestBed.flushEffects();

    expect(service.data()).toBeNull();
    expect(service.options()).toBeNull();
  });

  it('resets the filters to the default range', () => {
    start();
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));
    service.update({ employeeId: 3 });
    http.expectOne((candidate) => candidate.url === url).flush(analytics(1));

    service.reset();

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.has('employeeId')).toBeFalse();
    request.flush(analytics(1));
    expect(service.filter().employeeId).toBeNull();
  });
});
