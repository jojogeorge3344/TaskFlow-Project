import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { defaultFilter } from '../../core/analytics/analytics-filter';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { AnalyticsFilterOptions, AnalyticsFilterState, BucketCounts, TeamAnalytics } from '../../core/models';
import { ManagerAnalyticsService } from '../../core/services/manager-analytics.service';
import { systemPermissions } from '../../testing/permissions';
import { ManagerDashboardComponent } from './manager-dashboard.component';

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

function analytics(tasks: BucketCounts, teamSize = 3): TeamAnalytics {
  return {
    filter: { from: '2026-09-22', to: TODAY, basis: 'Created', departmentId: null, projectId: null, employeeId: null, managerId: null },
    summary: { teamSize, totalProjects: 1, tasks, totalTasks: tasks.total, completionPercent: tasks.completionPercent },
    tasksByStatus: tasks,
    tasksByPriority: [
      { priority: 'Low', count: 1 },
      { priority: 'Medium', count: 1 },
      { priority: 'High', count: 1 },
      { priority: 'Critical', count: 0 }
    ],
    byEmployee: [{ userId: 3, name: 'A. Thomas', tasks, highWorkload: true, hasOverdue: tasks.overdue > 0 }],
    byProject: [{ projectId: 4, name: 'Apollo', tasks, projectedEnd: null, outstanding: tasks.open }],
    attention: {
      delayedTasks: [
        { taskId: 231, taskCode: 'TF-231', title: 'Payment API retry', assigneeName: 'A. Thomas', projectName: 'Apollo', daysOverdue: 12 }
      ],
      projectsNeedingAttention: [{ projectId: 4, name: 'Apollo', overdueTasks: tasks.overdue }]
    }
  };
}

describe('ManagerDashboardComponent', () => {
  const user = signal<AuthUser | null>({
    id: 2,
    fullName: 'Riya Nair',
    username: 'manager',
    email: 'manager@taskflow.com',
    role: 'Manager',
    employeeCode: null,
    permissions: systemPermissions('Manager')
  });
  const options: AnalyticsFilterOptions = {
    today: TODAY,
    timezone: 'UTC',
    departments: [],
    projects: [{ id: 4, name: 'Apollo' }],
    employees: [{ id: 3, name: 'A. Thomas' }],
    managers: []
  };

  let filter: ReturnType<typeof signal<AnalyticsFilterState>>;
  let data: ReturnType<typeof signal<TeamAnalytics | null>>;
  let loading: ReturnType<typeof signal<boolean>>;
  let error: ReturnType<typeof signal<string | null>>;
  let service: { start: jasmine.Spy; update: jasmine.Spy; reset: jasmine.Spy; refresh: jasmine.Spy };
  let navigate: jasmine.Spy;

  function create(): { fixture: ComponentFixture<ManagerDashboardComponent>; element: HTMLElement } {
    const fixture = TestBed.createComponent(ManagerDashboardComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(async () => {
    filter = signal(defaultFilter(TODAY));
    data = signal<TeamAnalytics | null>(analytics(counts(5, 9, 18, 3)));
    loading = signal(false);
    error = signal<string | null>(null);
    service = {
      start: jasmine.createSpy('start'),
      update: jasmine.createSpy('update'),
      reset: jasmine.createSpy('reset'),
      refresh: jasmine.createSpy('refresh')
    };
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [ManagerDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: ManagerAnalyticsService,
          useValue: { ...service, filter, options: signal(options), data, loading, error, today: signal(TODAY) }
        },
        { provide: AuthService, useValue: { currentUser: user.asReadonly() } }
      ]
    }).compileComponents();

    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.callFake(navigate as never);
  });

  it('loads the analytics when it opens and greets the manager', () => {
    const { element } = create();

    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Riya Nair');
    expect(element.querySelector('.dashboard__subtitle')?.textContent).toContain('Team analytics');
    expect(element.querySelector('.dashboard__subtitle')?.textContent).toContain('Last 30 days by created date');
    expect(TestBed.inject(ManagerAnalyticsService).start).toHaveBeenCalled();
  });

  it('shows the team summary cards, which add up to the team total', () => {
    const { element } = create();

    const cards = Array.from(element.querySelectorAll('tf-kpi-card')).map((card) =>
      Array.from(card.querySelectorAll('.kpi__label, .kpi__value'))
        .map((part) => part.textContent?.trim())
        .join(' ')
    );

    expect(cards).toEqual(['Team tasks 35', 'Completed 18', 'In progress 9', 'Pending 5', 'Overdue 3', 'Completion 51%']);
    expect(18 + 9 + 5 + 3).toBe(35);
  });

  it('shows every manager section and no department view', () => {
    const { element } = create();

    for (const selector of [
      'tf-team-attention',
      'tf-team-task-analytics',
      'tf-project-analytics',
      'tf-team-employee-analytics',
      'tf-dashboard-filter-bar'
    ]) {
      expect(element.querySelector(selector)).withContext(selector).toBeTruthy();
    }
    expect(element.querySelector('tf-department-analytics')).toBeNull();
    expect(element.querySelector('#filter-department')).toBeNull();
    expect(element.querySelector('#filter-manager')).toBeNull();
    expect(element.textContent?.toLowerCase()).not.toContain('department-wise');
    expect(element.querySelector('[data-empty]')).toBeFalsy();
    expect(element.querySelector('[role="alert"]')).toBeFalsy();
  });

  it('offers only the in-scope employees and projects in the filters', () => {
    const { element } = create();

    const employees = Array.from(element.querySelectorAll('#filter-employee option')).map((option) => option.textContent?.trim());
    const projects = Array.from(element.querySelectorAll('#filter-project option')).map((option) => option.textContent?.trim());

    expect(employees).toEqual(['All', 'A. Thomas']);
    expect(projects).toEqual(['All', 'Apollo']);
  });

  it('passes filter changes and reset to the service', () => {
    const { element } = create();

    const employee = element.querySelector<HTMLSelectElement>('#filter-employee')!;
    employee.value = '3';
    employee.dispatchEvent(new Event('change'));
    element.querySelector<HTMLButtonElement>('[data-action="reset"]')!.click();

    expect(TestBed.inject(ManagerAnalyticsService).update).toHaveBeenCalledWith({ employeeId: 3 });
    expect(TestBed.inject(ManagerAnalyticsService).reset).toHaveBeenCalled();
  });

  it('shows loading placeholders in every section while a request is pending', () => {
    loading.set(true);
    const { element } = create();

    expect(element.querySelectorAll('tf-kpi-card .kpi__value--loading').length).toBe(6);
    expect(element.querySelector('tf-team-attention [role="status"]')).toBeTruthy();
    expect(element.querySelector('tf-project-analytics [role="status"]')).toBeTruthy();
    expect(element.querySelector('tf-team-employee-analytics [role="status"]')).toBeTruthy();
  });

  it('shows zeros and an empty message when the filters match no tasks', () => {
    data.set(analytics(counts(0, 0, 0, 0)));
    const { element } = create();

    expect(element.querySelector('[data-empty]')?.textContent).toContain('No team tasks match these filters');
    expect(element.querySelector('[data-no-team]')).toBeNull();
    expect(element.querySelector('tf-kpi-card')?.textContent).toContain('0');
    expect(element.querySelector('tf-team-task-analytics')?.textContent).toContain('No tasks match these filters.');
  });

  it('explains an empty team instead of a filter problem', () => {
    data.set({ ...analytics(counts(0, 0, 0, 0), 0), byEmployee: [], byProject: [] });
    const { element } = create();

    expect(element.querySelector('[data-no-team]')?.textContent).toContain('You have no team yet');
    expect(element.querySelector('[data-empty]')).toBeNull();
  });

  it('tells an administrator the dashboard is per-manager and links to their own dashboard', () => {
    user.set({ ...user()!, role: 'Administrator', permissions: systemPermissions('Administrator') });
    data.set({ ...analytics(counts(0, 0, 0, 0), 0), byEmployee: [], byProject: [] });
    const { element } = create();

    const note = element.querySelector('[data-admin-note]')!;
    expect(note.textContent).toContain('shows one manager');
    expect(note.querySelector('a')?.getAttribute('href')).toBe('/admin');
    expect(element.querySelector('[data-no-team]')?.textContent).not.toContain('Ask an administrator');
    user.set({ ...user()!, role: 'Manager', permissions: systemPermissions('Manager') });
  });

  it('shows an error with a retry and keeps the filter bar usable', () => {
    data.set(null);
    error.set('Could not load analytics. Check your connection and try again.');
    const { element } = create();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Could not load analytics');

    const basis = element.querySelector<HTMLSelectElement>('#filter-basis')!;
    expect(basis.disabled).toBeFalse();
    basis.value = 'Deadline';
    basis.dispatchEvent(new Event('change'));
    expect(TestBed.inject(ManagerAnalyticsService).update).toHaveBeenCalledWith({ basis: 'Deadline' });

    element.querySelector<HTMLButtonElement>('[data-action="retry"]')!.click();
    expect(TestBed.inject(ManagerAnalyticsService).refresh).toHaveBeenCalled();
  });

  it('opens the task list for a member and bucket, carrying the dashboard filters', () => {
    filter.set({ ...defaultFilter(TODAY), projectId: 4 });
    const { element } = create();

    element.querySelector<HTMLButtonElement>('tf-team-employee-analytics [data-segment="Overdue"]')!.click();

    expect(navigate).toHaveBeenCalledWith(['/tasks'], {
      queryParams: { from: '2026-09-22', to: TODAY, basis: 'Created', projectId: '4', employeeId: '3', bucket: 'Overdue' }
    });
  });

  it('links a delayed task in the summary to its record', () => {
    const { element } = create();

    const link = element.querySelector<HTMLAnchorElement>('tf-team-attention [data-delayed-task]')!;

    expect(link.getAttribute('href')).toBe('/tasks/231');
  });
});
