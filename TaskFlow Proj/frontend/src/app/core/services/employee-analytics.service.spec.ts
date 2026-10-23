import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { EmployeeOwnAnalytics } from '../models';
import { EmployeeAnalyticsService } from './employee-analytics.service';

describe('EmployeeAnalyticsService', () => {
  const url = '/api/v1/dashboard/employee/analytics';
  const buckets = { pending: 1, inProgress: 0, completed: 0, overdue: 0, total: 1, open: 1, completionPercent: 0 };

  function response(total: number): EmployeeOwnAnalytics {
    return {
      period: 'Last30Days',
      from: '2026-09-22',
      to: '2026-10-21',
      department: null,
      summary: { totalTasks: total, completionPercent: 0, completed: 0, outstanding: total },
      tasksByStatus: buckets,
      tasksByPriority: [],
      upcomingDeadlines: [],
      projects: [],
      trend: [],
      completedInPeriod: 0
    };
  }

  let service: EmployeeAnalyticsService;
  let http: HttpTestingController;
  let currentUser: ReturnType<typeof signal<unknown>>;

  beforeEach(() => {
    currentUser = signal<unknown>({ id: 1 });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { currentUser } }]
    });
    service = TestBed.inject(EmployeeAnalyticsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends only the period and never an employee or entity filter', () => {
    service.refresh();

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.keys()).toEqual(['period']);
    expect(request.request.params.get('period')).toBe('30d');
    request.flush(response(1));

    expect(service.data()?.summary.totalTasks).toBe(1);
    expect(service.loading()).toBeFalse();
  });

  it('reloads for a new period', () => {
    service.setPeriod('7d');

    const request = http.expectOne((candidate) => candidate.url === url);
    expect(request.request.params.get('period')).toBe('7d');
    request.flush(response(2));

    expect(service.period()).toBe('7d');
  });

  it('keeps only the newest response when the period changes while a request is in flight', () => {
    service.refresh();
    const first = http.expectOne((candidate) => candidate.url === url);
    service.setPeriod('7d');
    const second = http.expectOne((candidate) => candidate.url === url);

    second.flush(response(7));

    expect(first.cancelled).toBeTrue();
    expect(service.data()?.summary.totalTasks).toBe(7);
  });

  it('sets an error and then recovers on retry', () => {
    service.refresh();
    http.expectOne((candidate) => candidate.url === url).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(service.error()).toContain('Could not load');
    expect(service.loading()).toBeFalse();

    service.refresh();
    http.expectOne((candidate) => candidate.url === url).flush(response(3));

    expect(service.error()).toBeNull();
    expect(service.data()?.summary.totalTasks).toBe(3);
  });

  it('clears state when the user signs out', () => {
    service.refresh();
    http.expectOne((candidate) => candidate.url === url).flush(response(1));

    currentUser.set(null);
    TestBed.flushEffects();

    expect(service.data()).toBeNull();
    expect(service.period()).toBe('30d');
  });
});
