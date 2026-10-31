import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { defaultFilter } from '../../../shared/utils/analytics-filter';
import { AuthUser } from '../../../shared/auth/auth.models';
import { AuthService } from '../../../shared/auth/auth.service';
import { AdminAnalytics, AnalyticsFilterOptions, AnalyticsFilterState, BucketCounts } from '../analytics/models/analytics.model';
import { DashboardAnalyticsService } from '../analytics/services/dashboard-analytics.service';
import { systemPermissions } from '../../../shared/testing/permissions';
import { AdminDashboardComponent } from './admin-dashboard.component';

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

const TODAY = '2026-10-21';

function analytics(tasks: BucketCounts): AdminAnalytics {
  return {
    filter: { from: '2026-09-22', to: TODAY, basis: 'Created', departmentId: null, projectId: null, employeeId: null, managerId: null },
    summary: {
      totalEmployees: 48,
      totalManagers: 6,
      totalDepartments: 5,
      totalProjects: 9,
      tasks,
      totalTasks: tasks.total,
      completionPercent: tasks.completionPercent
    },
    tasksByStatus: tasks,
    tasksByPriority: [
      { priority: 'Low', count: 1 },
      { priority: 'Medium', count: 1 },
      { priority: 'High', count: 1 },
      { priority: 'Critical', count: 0 }
    ],
    deadlineBuckets: { overdue: tasks.overdue, dueToday: 0, next7Days: 0, later: 0 },
    trend: { granularity: 'day', points: [{ date: '2026-10-20', created: 1, completed: 1 }] },
    byDepartment: [{ departmentId: 1, name: 'Engineering', tasks }],
    byEmployee: { items: [{ userId: 3, name: 'A. Thomas', tasks }], totalEmployees: 1 },
    byManager: [{ managerId: 2, name: 'R. Nair', departments: 1, employees: 2, projects: 1, tasks }],
    byProject: [{ projectId: 4, name: 'Apollo', tasks, projectedEnd: null, outstanding: tasks.open }]
  };
}

describe('AdminDashboardComponent', () => {
  const user = signal<AuthUser | null>({
    id: 1,
    fullName: 'Admin User',
    username: 'admin',
    email: 'admin@taskflow.com',
    role: 'Administrator',
    employeeCode: null,
    permissions: systemPermissions('Administrator')
  });
  const options: AnalyticsFilterOptions = {
    today: TODAY,
    timezone: 'UTC',
    departments: [{ id: 1, name: 'Engineering' }],
    projects: [{ id: 4, name: 'Apollo' }],
    employees: [{ id: 3, name: 'A. Thomas' }],
    managers: [{ id: 2, name: 'R. Nair' }]
  };

  let filter: ReturnType<typeof signal<AnalyticsFilterState>>;
  let data: ReturnType<typeof signal<AdminAnalytics | null>>;
  let loading: ReturnType<typeof signal<boolean>>;
  let error: ReturnType<typeof signal<string | null>>;
  let service: { start: jasmine.Spy; update: jasmine.Spy; reset: jasmine.Spy; refresh: jasmine.Spy; setAllEmployees: jasmine.Spy };
  let navigate: jasmine.Spy;

  function create(): { fixture: ComponentFixture<AdminDashboardComponent>; element: HTMLElement } {
    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(async () => {
    filter = signal(defaultFilter(TODAY));
    data = signal<AdminAnalytics | null>(analytics(counts(5, 9, 18, 3)));
    loading = signal(false);
    error = signal<string | null>(null);
    service = {
      start: jasmine.createSpy('start'),
      update: jasmine.createSpy('update'),
      reset: jasmine.createSpy('reset'),
      refresh: jasmine.createSpy('refresh'),
      setAllEmployees: jasmine.createSpy('setAllEmployees')
    };
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: DashboardAnalyticsService,
          useValue: {
            ...service,
            filter,
            options: signal(options),
            data,
            loading,
            error,
            today: signal(TODAY),
            allEmployees: signal(false)
          }
        },
        { provide: AuthService, useValue: { currentUser: user.asReadonly() } }
      ]
    }).compileComponents();

    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.callFake(navigate as never);
  });

  it('loads the analytics when it opens and greets the administrator', () => {
    const { element } = create();

    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Admin User');
    expect(element.querySelector('.dashboard__subtitle')?.textContent).toContain('Last 30 days by created date');
    expect(TestBed.inject(DashboardAnalyticsService).start).toHaveBeenCalled();
  });

  it('shows the summary cards', () => {
    const { element } = create();

    const cards = Array.from(element.querySelectorAll('tf-kpi-card')).map((card) =>
      Array.from(card.querySelectorAll('.kpi__label, .kpi__value'))
        .map((part) => part.textContent?.trim())
        .join(' ')
    );

    expect(cards).toEqual([
      'Employees 48',
      'Managers 6',
      'Departments 5',
      'Projects 9',
      'Total tasks 35',
      'Completed 18',
      'In progress 9',
      'Pending 5',
      'Overdue 3',
      'Completion 51%'
    ]);
  });

  it('shows every analytics section', () => {
    const { element } = create();

    for (const selector of [
      'tf-department-analytics',
      'tf-task-analytics',
      'tf-employee-analytics',
      'tf-manager-analytics',
      'tf-project-analytics',
      'tf-dashboard-filter-bar'
    ]) {
      expect(element.querySelector(selector)).withContext(selector).toBeTruthy();
    }
    expect(element.querySelector('[data-empty]')).toBeFalsy();
    expect(element.querySelector('[role="alert"]')).toBeFalsy();
  });

  it('passes filter changes and reset to the service', () => {
    const { element } = create();

    const department = element.querySelector<HTMLSelectElement>('#filter-department')!;
    department.value = '1';
    department.dispatchEvent(new Event('change'));
    element.querySelector<HTMLButtonElement>('[data-action="reset"]')!.click();

    expect(TestBed.inject(DashboardAnalyticsService).update).toHaveBeenCalledWith({ departmentId: 1 });
    expect(TestBed.inject(DashboardAnalyticsService).reset).toHaveBeenCalled();
  });

  it('shows loading placeholders in every section while a request is pending', () => {
    loading.set(true);
    const { element } = create();

    expect(element.querySelectorAll('tf-kpi-card .kpi__value--loading').length).toBe(10);
    expect(element.querySelector('tf-department-analytics [role="status"]')).toBeTruthy();
    expect(element.querySelector('tf-manager-analytics [role="status"]')).toBeTruthy();
    expect(element.querySelector('tf-project-analytics [role="status"]')).toBeTruthy();
  });

  it('shows an empty state when the filters match no tasks', () => {
    data.set(analytics(counts(0, 0, 0, 0)));
    const { element } = create();

    expect(element.querySelector('[data-empty]')?.textContent).toContain('No tasks match these filters');
    expect(element.querySelector('tf-kpi-card')?.textContent).toContain('48');
    expect(element.querySelector('tf-department-analytics')?.textContent).toContain('No tasks match these filters.');
  });

  it('shows an error with a retry and keeps the filter bar usable', () => {
    data.set(null);
    error.set('Could not load analytics. Check your connection and try again.');
    const { element } = create();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Could not load analytics');
    const bar = element.querySelector('tf-dashboard-filter-bar');
    expect(bar).toBeTruthy();

    const basis = element.querySelector<HTMLSelectElement>('#filter-basis')!;
    expect(basis.disabled).toBeFalse();
    basis.value = 'Deadline';
    basis.dispatchEvent(new Event('change'));
    expect(TestBed.inject(DashboardAnalyticsService).update).toHaveBeenCalledWith({ basis: 'Deadline' });

    element.querySelector<HTMLButtonElement>('[data-action="retry"]')!.click();
    expect(TestBed.inject(DashboardAnalyticsService).refresh).toHaveBeenCalled();
  });

  it('opens the task list pre-filtered when a chart segment is clicked, carrying the dashboard filters', () => {
    filter.set({ ...defaultFilter(TODAY), departmentId: 1 });
    const { element } = create();

    element.querySelectorAll<HTMLButtonElement>('tf-department-analytics tbody tr:first-child .cell-button')[3].click();

    expect(navigate).toHaveBeenCalledWith(['/tasks'], {
      queryParams: {
        from: '2026-09-22',
        to: TODAY,
        basis: 'Created',
        departmentId: '1',
        bucket: 'Overdue'
      }
    });
  });
});
