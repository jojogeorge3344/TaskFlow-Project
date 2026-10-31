import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthUser } from '../../../shared/auth/auth.models';
import { AuthService } from '../../../shared/auth/auth.service';
import { BucketCounts, EmployeeOwnAnalytics, EmployeePeriod } from '../analytics/models/analytics.model';
import { EmployeeAnalyticsService } from '../analytics/services/employee-analytics.service';
import { systemPermissions } from '../../../shared/testing/permissions';
import { EmployeeDashboardComponent } from './employee-dashboard.component';

function counts(pending: number, inProgress: number, completed: number, overdue: number): BucketCounts {
  const total = pending + inProgress + completed + overdue;
  return {
    pending,
    inProgress,
    completed,
    overdue,
    total,
    open: pending + inProgress + overdue,
    completionPercent: total === 0 ? 0 : Math.round((completed / total) * 100)
  };
}

function analytics(overrides: Partial<EmployeeOwnAnalytics> = {}): EmployeeOwnAnalytics {
  return {
    period: 'Last30Days',
    from: '2026-09-22',
    to: '2026-10-21',
    department: { name: 'Engineering', limited: false, totalTasks: 120, tasks: counts(9, 34, 72, 5) },
    summary: { totalTasks: 12, completionPercent: 42, completed: 5, outstanding: 7 },
    tasksByStatus: counts(2, 3, 5, 2),
    tasksByPriority: [
      { priority: 'Low', count: 3 },
      { priority: 'Medium', count: 6 },
      { priority: 'High', count: 3 },
      { priority: 'Critical', count: 0 }
    ],
    upcomingDeadlines: [
      { taskId: 301, taskCode: 'TF-301', title: 'Update API docs', priority: 'High', deadline: '2026-10-18T09:00:00Z', daysRemaining: -3, isOverdue: true, daysOverdue: 3 },
      { taskId: 312, taskCode: 'TF-312', title: 'Review test plan', priority: 'Medium', deadline: '2026-10-21T17:00:00Z', daysRemaining: 0, isOverdue: false, daysOverdue: 0 },
      { taskId: 320, taskCode: 'TF-320', title: 'Sprint demo prep', priority: 'Low', deadline: '2026-10-25T09:00:00Z', daysRemaining: 4, isOverdue: false, daysOverdue: 0 }
    ],
    projects: [{ projectId: 7, name: 'Apollo', totalTasks: 5, completedTasks: 3, progressPercent: 60, latestDeadline: '2026-10-24T09:00:00Z' }],
    trend: [
      { date: '2026-10-20', created: 1, completed: 0 },
      { date: '2026-10-21', created: 0, completed: 3 }
    ],
    completedInPeriod: 3,
    ...overrides
  };
}

describe('EmployeeDashboardComponent', () => {
  const userSignal = signal<AuthUser | null>({
    id: 3,
    fullName: 'Employee User',
    username: 'employee',
    email: 'employee@taskflow.com',
    role: 'Employee',
    employeeCode: null,
    permissions: systemPermissions('Employee')
  });

  const data = signal<EmployeeOwnAnalytics | null>(null);
  const loading = signal(false);
  const error = signal<string | null>(null);
  const period = signal<EmployeePeriod>('30d');
  let refresh: jasmine.Spy;
  let setPeriod: jasmine.Spy;

  function create(): ComponentFixture<EmployeeDashboardComponent> {
    const fixture = TestBed.createComponent(EmployeeDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    data.set(analytics());
    loading.set(false);
    error.set(null);
    period.set('30d');
    refresh = jasmine.createSpy('refresh');
    setPeriod = jasmine.createSpy('setPeriod');

    await TestBed.configureTestingModule({
      imports: [EmployeeDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser: userSignal.asReadonly() } },
        {
          provide: EmployeeAnalyticsService,
          useValue: { data, loading, error, period, refresh, setPeriod }
        }
      ]
    }).compileComponents();
  });

  it('loads on open and shows the welcome line', () => {
    const element = create().nativeElement as HTMLElement;

    expect(refresh).toHaveBeenCalled();
    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Employee User');
  });

  it('offers only the period selector and no entity filters', () => {
    const element = create().nativeElement as HTMLElement;

    const chips = Array.from(element.querySelectorAll('.period__chip')).map((chip) => chip.textContent?.trim());
    expect(chips).toEqual(['Last 7 days', 'Last 30 days', 'This month']);
    expect(element.querySelector('select')).toBeNull();
    expect(element.querySelector('tf-dashboard-filter-bar')).toBeNull();
    expect(element.querySelector('.period__chip[aria-pressed="true"]')?.textContent).toContain('Last 30 days');
  });

  it('changes the period through the service', () => {
    const element = create().nativeElement as HTMLElement;

    (element.querySelectorAll('.period__chip')[0] as HTMLButtonElement).click();

    expect(setPeriod).toHaveBeenCalledWith('7d');
  });

  it('summary: shows the counts, completion and completed versus outstanding', () => {
    const element = create().nativeElement as HTMLElement;

    const values = Array.from(element.querySelectorAll('tf-kpi-card .kpi__value')).map((el) => el.textContent?.trim());
    expect(values).toEqual(['12', '5', '3', '2', '2', '42%']);
    expect(element.querySelector('[data-summary-text]')?.textContent).toMatch(/5\s*completed.*7\s*outstanding.*42%/s);
  });

  it('deadlines: lists soonest first, links to the task and writes Overdue in text', () => {
    const element = create().nativeElement as HTMLElement;

    const rows = Array.from(element.querySelectorAll('[data-section="deadlines"] .dl__row'));
    expect(rows.length).toBe(3);
    expect(rows[0].getAttribute('href')).toBe('/tasks/301');
    expect(rows[0].textContent).toContain('3 days overdue');
    expect(rows[1].textContent).toContain('Due today');
    expect(rows[2].textContent).toContain('In 4 days');
  });

  it('trend: shows the completed-in-period count', () => {
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelector('[data-completed-in-period]')?.textContent?.trim()).toBe('3');
  });

  it('projects: lists the employee’s own progress per project', () => {
    const element = create().nativeElement as HTMLElement;

    const section = element.querySelector('[data-section="projects"]') as HTMLElement;
    expect(section.textContent).toContain('Apollo');
    expect(section.textContent).toContain('60%');
    expect(section.textContent).toContain('3 of 5 of your tasks done');
  });

  it('department: shows aggregates only, with no colleague names', () => {
    const element = create().nativeElement as HTMLElement;

    const section = element.querySelector('[data-section="department"]') as HTMLElement;
    expect(section.textContent).toContain('Engineering');
    const values = Array.from(section.querySelectorAll('[data-dept-stats] strong')).map((el) => el.textContent?.trim());
    expect(values).toEqual(['120', '72', '34', '9', '5']);
    expect(section.querySelector('table')).toBeNull();
  });

  it('department: a small department shows no task figures', () => {
    data.set(analytics({ department: { name: 'Tiny', limited: true, totalTasks: null, tasks: null } }));
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelector('[data-dept-stats]')).toBeNull();
    expect(element.querySelector('[data-dept-limited]')?.textContent).toContain('do not show task figures');
  });

  it('empty: no department shows "No department assigned"', () => {
    data.set(analytics({ department: null }));
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelector('[data-no-department]')?.textContent).toContain('No department assigned');
  });

  it('empty: no projects and no tasks show friendly messages without errors', () => {
    data.set(
      analytics({
        summary: { totalTasks: 0, completionPercent: 0, completed: 0, outstanding: 0 },
        tasksByStatus: counts(0, 0, 0, 0),
        tasksByPriority: [],
        upcomingDeadlines: [],
        projects: [],
        trend: [],
        completedInPeriod: 0
      })
    );
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelector('[data-empty]')?.textContent).toContain('You have no tasks yet');
    expect(element.querySelector('[data-section="projects"]')?.textContent).toContain('You have no projects yet');
    expect(element.querySelector('[data-deadlines-empty]')).toBeTruthy();
    expect(element.querySelector('[data-action="retry"]')).toBeNull();
  });

  it('loading: shows placeholders instead of numbers', () => {
    loading.set(true);
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelectorAll('tf-kpi-card .kpi__value--loading').length).toBe(6);
    expect(element.querySelector('[data-section="deadlines"] .tf-skeleton')).toBeTruthy();
  });

  it('error: shows a message with Retry and keeps the period selector usable', () => {
    data.set(null);
    error.set('Could not load your dashboard.');
    const element = create().nativeElement as HTMLElement;

    expect(element.querySelector('.dashboard__error')?.textContent).toContain('Could not load your dashboard.');
    expect(element.querySelectorAll('.period__chip').length).toBe(3);

    refresh.calls.reset();
    (element.querySelector('[data-action="retry"]') as HTMLButtonElement).click();
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('drill-down: a slice opens the caller’s own task list for that bucket', () => {
    const fixture = create();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture.componentInstance.openTasks({ bucket: 'Overdue' });

    expect(navigate).toHaveBeenCalledWith(['/tasks'], { queryParams: { employeeId: '3', bucket: 'Overdue' } });
  });

  it('drill-down: a trend point opens the tasks completed on that day', () => {
    const fixture = create();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture.componentInstance.onTrendPoint({ seriesKey: 'completed', index: 1, label: '21 Oct', value: 3 });

    expect(navigate).toHaveBeenCalledWith(['/tasks'], {
      queryParams: { employeeId: '3', from: '2026-10-21', to: '2026-10-21', basis: 'Completed', bucket: 'All' }
    });
  });
});
